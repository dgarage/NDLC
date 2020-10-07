module NDLC.GUI.DLCAcceptModule

open System
open System.Text

open FSharp.Control.Tasks

open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.FuncUI.DSL
open Avalonia.Layout

open System
open Avalonia.FuncUI.Types
open Elmish
open NBitcoin.DataEncoders
open NDLC
open NDLC.GUI.Utils
open NDLC.Infrastructure
open NDLC.Messages
open Newtonsoft.Json
open ResultUtils
open TaskUtils


type State = {
    OfferMsg: string
    ReviewResult: string option
    SetupPSBT: string
    ErrorMsg: string option
    ReviewMsg: string option
    ParsedOffer: Offer option
}
    with
    member this.ValidateOfferMsg() =
        if (this.OfferMsg |> String.IsNullOrEmpty) then Some "Empty Offer msg not allowed" else
        // if (String.isBase64 this.OfferMsg |> not) then Some "Offer must be base64 encoded" else
        None
        
    member this.ValidatePSBT() =
        if (this.SetupPSBT |> String.IsNullOrEmpty) then Some "Empty PSBT not allowed" else
        // if (String.isBase64 this.SetupPSBT  |> not) then Some "PSBT must be base64 encoded" else
        None
        
    member this.TryGetCollateral() =
        this.ParsedOffer |> Option.map(fun o -> DiscretePayoffs.CreateFromContractInfo(o.ContractInfo, o.TotalCollateral))
        
        

type InternalMsg =
    | OfferMsgUpdate of string
    | PSBTUpdate of string
    | InvalidInput of msg: string
    | Reset
    | ResetErrorMsg
    | ReviewMsg of msg: string
    | OfferParsed of o: Offer
    | TryReview
    | ConfirmReview
    
type OutMsg =
    | Finished of msg: string
    
type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg
    
type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
    OnInputFinished:  string -> 'Msg
}

type Translator<'Msg> = Msg -> 'Msg

let translator ({ OnInternalMsg = onInternalMsg; OnInputFinished = onInputFinished }: TranslationDictionary<'Msg>): Translator<'Msg> =
    function
    | ForSelf i -> onInternalMsg i
    | ForParent(Finished msg) -> onInputFinished msg
    

let init = {
    OfferMsg = ""; SetupPSBT = ""; ErrorMsg = None; ReviewResult = None
    ReviewMsg = None; ParsedOffer = None
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
            |> Ok
    }

let update globalConfig msg state =
    match msg with
    | OfferMsgUpdate msg ->
        { state with OfferMsg = msg }, Cmd.none
    | PSBTUpdate msg ->
        { state with SetupPSBT = msg }, Cmd.none
    | InvalidInput msg ->
        { state with ErrorMsg = Some (msg) }, Cmd.none
    | Reset ->
        init, Cmd.none
    | ResetErrorMsg ->
        { state with ErrorMsg = None }, Cmd.none
    | TryReview ->
        let onError(e: exn) = InvalidInput (e.Message) |> ForSelf
        let onSuccess = ReviewMsg >> ForSelf
        state, (Cmd.OfTask.either) (Tasks.review >> Task.map Result.deref) (globalConfig, state.OfferMsg) (onSuccess) (onError)
    | ReviewMsg msg ->
        { state with ReviewMsg = Some msg; ErrorMsg = None }, Cmd.none
    | ConfirmReview ->
        { state with ReviewMsg = None }, Cmd.OfFunc.perform(parseOfferMsg globalConfig) (state.OfferMsg) (function | Ok s -> OfferParsed(s) |> ForSelf | Error e -> InvalidInput e |> ForSelf)
    | OfferParsed o ->
        { state with ParsedOffer = Some o }, Cmd.none
        
let private offerView (state: State) dispatch =
    StackPanel.create [
        StackPanel.children  [
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.name "Base64Offer"
                TextBox.useFloatingWatermark true
                TextBox.watermark (sprintf "Paste Base64-encoded offer message here")
                TextBox.height 120.
                TextBox.errors (state.ValidateOfferMsg() |> Option.toList |> Seq.cast<obj>)
                TextBox.text (state.OfferMsg)
                yield! TextBox.onTextInput(OfferMsgUpdate >> ForSelf >> dispatch)
            ]
            Button.create [
                Button.isEnabled (state.ValidateOfferMsg().IsNone)
                Button.classes [ "round" ]
                Button.content "Review"
                Button.onClick(fun _ -> TryReview |> ForSelf |> dispatch)
            ]
        ]
    ]
            
        
let private reviewMsgView (state: string option) dispatch =
    StackPanel.create [
        StackPanel.isVisible (state.IsSome)
        StackPanel.children [
            TextBlock.create [
                TextBlock.text (state |> function Some x -> x | None -> "")
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
    
let private psbtView (g) (state: State) dispatch =
        let v = state.SetupPSBT |> Option.ofObj |> Option.map(fun  x -> validatePSBT(x, g.Network))
        [
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.name "Setup PSBT"
                TextBox.useFloatingWatermark true
                TextBox.watermark (sprintf "Paste your PSBT here! %A" (state.TryGetCollateral()))
                TextBox.height 120.
                TextBox.errors (v |> Option.toList |> Seq.cast<obj>)
                TextBox.text (state.SetupPSBT)
                yield! TextBox.onTextInput(PSBTUpdate >> ForSelf >> dispatch)
            ] :> IView
            StackPanel.create [
                StackPanel.orientation Orientation.Horizontal
                StackPanel.children [
                    Button.create[
                        Button.isEnabled (v.IsNone)
                        Button.content "Cancel"
                    ]
                    Button.create[
                        Button.isEnabled (v.IsNone)
                        Button.content "Confirm"
                        Button.onClick(fun _ -> ConfirmReview |> ForSelf |> dispatch)
                    ]
                ]
            ] :> IView
        ]


let view globalConfig (state: State) dispatch =
    StackPanel.create [
        StackPanel.horizontalAlignment HorizontalAlignment.Center
        StackPanel.verticalAlignment VerticalAlignment.Center
        StackPanel.width 450.
        StackPanel.margin 10.
        StackPanel.children [
            (offerView state dispatch)
            (reviewMsgView state.ReviewMsg dispatch)
            yield! (psbtView globalConfig (state) dispatch)
        ]
    ]
