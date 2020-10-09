[<RequireQualifiedAccess>]
module NDLC.GUI.DLCAcceptModule

open System
open System.Text

open FSharp.Control.Tasks

open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.Layout

open System.Diagnostics
open Avalonia.FuncUI.Types
open Elmish
open NBitcoin.DataEncoders
open NDLC
open NDLC.GUI.Utils
open NDLC.Infrastructure
open NDLC.Messages
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open ResultUtils
open TaskUtils

type private BeforeReviewState =
    { OfferMsg: string; }
    
type private ReviewingState = {
    ReviewMsg: string
    Offer: Offer
    LocalName: string
}
type private AfterReviewState = {
    PSBT: string
    Offer: Offer
    LocalName: string
    DLC: Repository.DLCState
}
    with
    member this.GetCollateral() =
        this.Offer.ToDiscretePayoffs().Inverse().CalculateMinimumCollateral()
type StepState =
    private
    | BeforeReview of BeforeReviewState
    | Reviewing of ReviewingState
    | AfterReview of AfterReviewState
    
type State = {
    ErrorMsg: string option
    Step: StepState
}

type AcceptResult = {
    Msg: string
    AcceptBase64: string
    AcceptJson: string
}
type InternalMsg =
    // state independent
    | InvalidInput of msg: string
    | Reset
    | ResetErrorMsg
    
    // before review
    | OfferMsgUpdate of string
    | TryReview
    | StartReview of o: Offer * msg: string
    
    // reviewing
    | LocalNameUpdate of string
    | ConfirmReview
    | ReviewAccepted of Repository.DLCState
    
    // after review
    | PSBTUpdate of string
    | TrySetupPSBT
    | FinishOperation of AcceptResult
    
type OutMsg =
    | Finished of AcceptResult
    
type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg
    
type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
    OnInputFinished: AcceptResult -> 'Msg
}

type Translator<'Msg> = Msg -> 'Msg

let translator ({ OnInternalMsg = onInternalMsg; OnInputFinished = onInputFinished }: TranslationDictionary<'Msg>): Translator<'Msg> =
    function
    | ForSelf i -> onInternalMsg i
    | ForParent(Finished msg) -> onInputFinished msg
    

let init = {
    ErrorMsg = None
    Step = BeforeReview { OfferMsg = "" }
}

[<AutoOpen>]
module private Helpers =

    let tryParseOfferMsg globalConfig (offer: string)=
        if (offer |> String.IsNullOrWhiteSpace) then None else
        let r = ConfigUtils.repository globalConfig
        JsonConvert.DeserializeObject<Offer>(UTF8Encoding.UTF8.GetString(Encoders.Base64.DecodeData(offer)), r.JsonSettings)
        |> fun x -> (printfn "Deserialized result is %A" x ; x)
        |> Option.ofObj
        
    let parseOfferMsg g offerMsg = 
        match tryParseOfferMsg g offerMsg with
        | None -> Error("Failed to parse Offer message")
        | Some o ->
            if (o.OracleInfo |> isNull) then Error "Invalid Offer Message! Missing OracleInfo" else
            if (o.Timeouts |> isNull) then Error "Invalid Offer Message! Missing Timeouts" else
            if (o.ContractInfo |> isNull) then Error "Invalid Offer Message! Missing ContractInfo" else
            Ok(o)
            
    let validateOfferMsg offerMsg =
        if (offerMsg |> String.IsNullOrWhiteSpace) then Some "Empty Offer msg not allowed" else
        // if (String.isBase64 this.OfferMsg |> not) then Some "Offer must be base64 encoded" else
        None
            
    let validatePSBTInContext g offerMsg psbt =
        let o = tryParseOfferMsg g offerMsg
        match tryParsePSBT (psbt, g.Network), (o |> Option.map(fun o -> o.ToDiscretePayoffs())) with
        | Error x, _ -> Some x
        | Ok psbt, Some cInfo ->
            let c = cInfo.CalculateMinimumCollateral()
            if (psbt.Outputs |> Seq.exists(fun o -> o.Value = c)) then
                None
            else
                Some (sprintf "The setup psbt must send %d Satoshis to yourself" c.Satoshi)
        | _ -> None
module private Tasks =
    let review (g: GlobalConfig, offerStr: string) = task {
        
        match parseOfferMsg g offerStr with
        | Error e -> return Error e
        | Ok o ->
        let repo = ConfigUtils.repository g
        let nRepo = ConfigUtils.nameRepo g
        let! oracle = repo.GetOracle(o.OracleInfo.PubKey)
        if (oracle |> isNull) then return Error(sprintf "Unknown Oracle!") else
        let! oracleName = nRepo.GetName(Scopes.Oracles, OracleId(o.OracleInfo.PubKey).ToString())
        if (oracleName |> isNull) then return Error ("Unknown Oracle") else
        let! evt = repo.GetEvent(o.OracleInfo.PubKey, o.OracleInfo.RValue)
        if (evt |> isNull || evt.EventId |> isNull) then return Error ("Unknown Event") else
        let maturity = NDLC.LockTimeEstimation(o.Timeouts.ContractMaturity, g.Network);
        let refund = NDLC.LockTimeEstimation(o.Timeouts.ContractTimeout, g.Network);
        if (refund.UnknownEstimation |> not) && (refund.EstimatedRemainingBlocks = 0) then
            return Error ("The refund should not be immediately valid")
        else if (refund.UnknownEstimation |> not) && (refund.EstimatedRemainingBlocks < maturity.EstimatedRemainingBlocks) then
            return Error ("The refund should not be valid faster than the contract execution transactions")
        else if (not <| o.SetContractPreimages(evt.Outcomes)) then
             return Error("The contract info of the offer does not match the specification of the event")
        else
        let! t = nRepo.AsEventRepository().ResolveName(evt.EventId)
        let evtName = t |> Option.ofObj |> Option.defaultValue (EventFullName("???", "???");)
    
        let printPayoffs(payoffs: OfferReview.Payoff seq) =
            let sb = StringBuilder()
            for p in payoffs do
                sb.AppendLine(sprintf "%s <= %A" (p.Reward.ToString(true, false)) (p.Outcome)) |> ignore
            sb.ToString()
        let review = OfferReview(o);
        return
            (sprintf "Event: %A\n" evtName) +
            (sprintf "The payoff function if you accept: %s \n" (printPayoffs review.AcceptorPayoffs)) +
            (sprintf "Your expected collateral: %s\n" (review.AcceptorCollateral.ToString(false, false))) +
            (sprintf "Contract Execution validity: %s\n" (maturity.ToString())) +
            (sprintf "Refund validity: %s\n" (refund.ToString())) + 
            ("If you are sure you want to accept this offer, ") +
            (sprintf "you must prepare psbt to fund %s BTC to yourself" (review.AcceptorCollateral.ToString(false, false)))
            |> fun msg -> Ok(o, msg)
    }
    
    let accept g (localName: string, offer: Offer) = task {
        let! dlc = CommandBase.tryGetDLC g (localName)
        match dlc with
        | Some _ -> 
            return failwith "The DLC with the same name already exists"
        | None ->
        let repo = ConfigUtils.repository g
        let! evt = repo.GetEvent(offer.OracleInfo.PubKey, offer.OracleInfo.RValue)
        offer.SetContractPreimages(evt.Outcomes) |> ignore
        let builder = DLCTransactionBuilder(false, null, null, null, g.Network)
        builder.Accept(offer) |> ignore
        let! dlc = repo.NewDLC(offer.OracleInfo, builder);
        dlc.BuilderState <- builder.ExportStateJObject();
        dlc.Offer <- JObject.FromObject(offer, JsonSerializer.Create(repo.JsonSettings))
        let nRepo = ConfigUtils.nameRepo g
        do! nRepo.AsDLCNameRepository().SetMapping(localName, dlc.Id);
        do! repo.SaveDLC(dlc);
        return dlc
    }
    let setup g (psbt: string, dlc: Repository.DLCState) = task {
        let psbt = tryParsePSBT(psbt, g.Network)
        match psbt with
        | Error e -> return failwithf "Failed to parse PSBT! %s" (e)
        | Ok psbt ->
        let builder = dlc.GetBuilder(g.Network)
        let repo = ConfigUtils.repository g
        let! (keyPath, key) = repo.CreatePrivateKey()
        let accept = builder.FundAccept(key, psbt)
        accept.AcceptorContractId <- dlc.Id
        dlc.FundKeyPath <- keyPath;
        dlc.Abort <- psbt;
        dlc.BuilderState <- builder.ExportStateJObject()
        let jAccept = JObject.FromObject(accept, JsonSerializer.Create(repo.JsonSettings))
        dlc.Accept <- jAccept
        do! repo.SaveDLC(dlc)
        let jAcceptString = JsonConvert.SerializeObject(accept, repo.JsonSettings);
        let base64Accept =
            UTF8Encoding.UTF8.GetString(Encoders.Base64.DecodeData(jAcceptString))
        return (base64Accept, jAcceptString)
    }

let update globalConfig msg state =
    match msg, state.Step with
    // ----- state independent ---
    | InvalidInput msg, _ ->
        { state with ErrorMsg = Some (msg) }, Cmd.none
    | Reset, _ ->
        init, Cmd.none
    | ResetErrorMsg, _ ->
        { state with ErrorMsg = None }, Cmd.none
        
    // ----- before review -----
    | OfferMsgUpdate msg, BeforeReview s ->
        { state with Step = BeforeReview { s with OfferMsg = msg }}, Cmd.none
    | TryReview, BeforeReview s ->
        let onError(e: exn) = InvalidInput (e.ToString()) |> ForSelf
        let onSuccess = StartReview >> ForSelf
        state, (Cmd.OfTask.either) (Tasks.review >> Task.map Result.deref) (globalConfig, s.OfferMsg) (onSuccess) (onError)
    | StartReview (o, msg), BeforeReview _ ->
        { state with Step = Reviewing { Offer = o; ReviewMsg = msg; LocalName = "" } }, Cmd.none
        
    // ----- reviewing -----
    | LocalNameUpdate msg, Reviewing s ->
        { state with Step =  Reviewing { s with LocalName = msg } }, Cmd.none
    | ConfirmReview, Reviewing s ->
        let onSuccess = (ReviewAccepted >> ForSelf)
        let onError (e: exn) = (e.Message |> InvalidInput |> ForSelf)
        state,
        Cmd.OfTask.either (Tasks.accept globalConfig) (s.LocalName, s.Offer) (onSuccess) (onError)
    | ReviewAccepted dlc, Reviewing s ->
        { state with ErrorMsg = None; Step = AfterReview { PSBT = ""; Offer = s.Offer; LocalName = s.LocalName; DLC = dlc } },
        Cmd.none
        
    // ----- after review (setup) -----
    | PSBTUpdate msg, AfterReview s ->
        { state with Step =  AfterReview { s with PSBT = msg } }, Cmd.none
    | TrySetupPSBT, AfterReview s ->
        let onSuccess = (fun (accept, jAccept) -> FinishOperation({ Msg = "Send this Accept message to other peer!"; AcceptBase64 = accept; AcceptJson = jAccept }) |> ForSelf)
        let onFailure (e: exn) =  (e.Message |> InvalidInput |> ForSelf)
        state, Cmd.OfTask.either (Tasks.setup globalConfig) (s.PSBT, s.DLC) (onSuccess) onFailure
    | FinishOperation (result), AfterReview s ->
        { state with ErrorMsg = None; Step = AfterReview { s with PSBT = ""; Offer = s.Offer; LocalName = s.LocalName; } },
        Cmd.ofMsg (Finished (result) |> ForParent)
    | _ ->
        Debug.Assert(false, "Unreachable")
        state, Cmd.none
        
let private offerView ({ OfferMsg = offerMsg }) dispatch =
    StackPanel.create [
        StackPanel.children  [
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.name "Base64Offer"
                TextBox.useFloatingWatermark true
                TextBox.watermark (sprintf "Paste Base64-encoded offer message here")
                TextBox.height 120.
                TextBox.errors (validateOfferMsg(offerMsg) |> Option.toList |> Seq.cast<obj>)
                TextBox.text (offerMsg)
                yield! TextBox.onTextInput(OfferMsgUpdate >> ForSelf >> dispatch)
            ]
            Button.create [
                Button.isEnabled (validateOfferMsg(offerMsg).IsNone)
                Button.classes [ "round" ]
                Button.content "Review"
                Button.onClick(fun _ -> TryReview |> ForSelf |> dispatch)
            ]
        ]
    ]
            
        
let private reviewMsgView (state: ReviewingState) dispatch =
    StackPanel.create [
        StackPanel.children [
            TextBlock.create [
                TextBlock.text (state.ReviewMsg)
            ]
            let localNameError = if state.LocalName |> String.IsNullOrWhiteSpace then Some ("You must specify local name") else None
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.name "LocalName"
                TextBox.useFloatingWatermark true
                TextBox.watermark (sprintf "Set local name to identify this DLC" )
                TextBox.errors (localNameError |> Option.toList |> Seq.cast<obj>)
                TextBox.text (state.LocalName)
                yield! TextBox.onTextInput(LocalNameUpdate >> ForSelf >> dispatch)
            ]
            
            StackPanel.create [
                StackPanel.orientation Orientation.Horizontal
                StackPanel.dock Dock.Right
                StackPanel.children [
                    Button.create[
                        Button.classes ["round"]
                        Button.content "Cancel"
                        Button.onClick(fun _ -> Reset |> ForSelf |> dispatch)
                    ]
                    Button.create[
                        Button.classes ["round"]
                        Button.content "Confirm"
                        Button.onClick(fun _ -> ConfirmReview |> ForSelf |> dispatch)
                    ]
                ]
            ]
        ]
    ]
    
let private psbtView (g) (state: AfterReviewState) dispatch =
    StackPanel.create [
        StackPanel.children [
            let psbtError = state.PSBT |> Option.ofObj |> Option.bind(fun  x -> validatePSBT(x, g.Network))
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.name "Setup PSBT"
                TextBox.useFloatingWatermark true
                TextBox.watermark (sprintf "Paste your PSBT here! It must pay %A BTC to yourself" (state.GetCollateral()))
                TextBox.height 120.
                TextBox.errors (psbtError |> Option.toList |> Seq.cast<obj>)
                TextBox.text (state.PSBT)
                yield! TextBox.onTextInput(PSBTUpdate >> ForSelf >> dispatch)
            ] :> IView
            StackPanel.create [
                StackPanel.orientation Orientation.Horizontal
                StackPanel.children [
                    Button.create[
                        Button.content "Cancel"
                        Button.onClick(fun _ -> Reset |> ForSelf |> dispatch)
                    ]
                    Button.create[
                        Button.isEnabled (psbtError.IsNone)
                        Button.content "Confirm"
                        Button.onClick(fun _ -> TrySetupPSBT |> ForSelf |> dispatch)
                    ]
                ]
            ] :> IView
        ]
    ]


let view globalConfig (state: State) dispatch =
    StackPanel.create [
        StackPanel.horizontalAlignment HorizontalAlignment.Center
        StackPanel.verticalAlignment VerticalAlignment.Center
        StackPanel.width 450.
        StackPanel.margin 10.
        StackPanel.children [
            match state.Step with
            | BeforeReview s ->
                (offerView s dispatch)
            | Reviewing s ->
                (reviewMsgView s dispatch)
            | AfterReview s ->
                (psbtView globalConfig (s) dispatch)
                
            TextBlock.create [
                TextBlock.classes ["error"]
                TextBlock.text (state.ErrorMsg |> Option.toObj)
                TextBlock.isVisible(state.ErrorMsg.IsSome)
            ]
        ]
    ]
