[<RequireQualifiedAccess>]
module NDLC.GUI.DLCOfferModule

open System
open System.Linq
open FSharp.Control.Tasks
open Avalonia.Controls
open Elmish
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open NBitcoin

open TaskUtils
open ResultUtils

open NDLC.Infrastructure
open NDLC.Messages

open System.Collections.Generic
open System.Text
open NBitcoin.DataEncoders
open System.Threading.Tasks
open NDLC
open NDLC.GUI
open NDLC.GUI.GlobalMsgs
open NDLC.GUI.Utils
open Newtonsoft.Json
open Newtonsoft.Json.Linq

type ErrorState = private {
    ContractInfoErr: string option
    /// The locktime of the CET transaction
    LockTimeErr: string option
    RefundLockTimeErr: string option
    EventFullNameErr: string option
    LocalNameErr: string option
    FeeRateErr: string option
    PSBTErr: string option
}

type OfferDomainModel = {
    ContractInfo: DiscretePayoffs
    /// The locktime of the CET transaction
    LockTime: LockTime
    RefundLockTime: LockTime
    EventFullName: EventFullName
    LocalName: string
    FeeRate: FeeRate
    PSBT: PSBT
}

type OfferVM = {
    ContractInfo: string
    /// The locktime of the CET transaction
    LockTime: string
    RefundLockTime: string
    EventFullName: string
    LocalName: string
    FeeRate: string
    SetupPSBT: string
}
    with
    static member Empty = {
        ContractInfo = ""
        LockTime = "0"
        RefundLockTime = "499999999"
        EventFullName = ""
        LocalName = ""
        FeeRate = ""
        SetupPSBT = ""
    }
    static member FromMetadata(m: NewOfferMetadata) = {
        OfferVM.Empty with
            ContractInfo = m.Outcomes |> fun x -> String.Join(",", x)
            EventFullName = m.EventFullName.ToString()
    }
    
    member this.Validate(n): ErrorState =
        let contractInfoR = 
                tryParseContractInfo this.ContractInfo
        {
            ContractInfoErr =
                validateContractInfo this.ContractInfo
            LocalNameErr =
                if (this.LocalName |> String.IsNullOrWhiteSpace) then Some("You must specify LocalName") else
                if this.LocalName.Length > 20 then Some ("You can not specify local name with more than 20 characters") else None
            LockTimeErr =
                validateLockime this.LockTime
            RefundLockTimeErr =
                validateLockime this.RefundLockTime
            EventFullNameErr =
                validateEventFullName this.EventFullName
            FeeRateErr =
                validateFeeRate this.FeeRate
            PSBTErr =
                match tryParsePSBT (this.SetupPSBT, n), contractInfoR with
                | Error x, _ -> Some x
                | Ok psbt, Ok cInfo ->
                    let c = cInfo.CalculateMinimumCollateral()
                    if (psbt.Outputs |> Seq.exists(fun o -> o.Value = c)) then
                        None
                    else
                        Some (sprintf "The setup psbt must send %d Satoshis to yourself" c.Satoshi)
                | _ -> None
        }
        
    member this.TryGetCollateral() =
        tryParseContractInfo this.ContractInfo
        |> Result.map(fun x -> x.CalculateMinimumCollateral())
        
    member this.ToDomainModel(globalConfig) =
        result {
            let! c =  tryParseContractInfo this.ContractInfo
            let! localName = if (this.LocalName |> String.IsNullOrEmpty) then Error("You must specify LocalName") else Ok(this.LocalName)
            let! lockTime = tryParseLockTime this.LockTime
            let! rLockTime = tryParseLockTime this.RefundLockTime
            let! f = tryParseFeeRate this.FeeRate
            let! e = tryParseEventFullname this.EventFullName
            let! psbt = tryParsePSBT (this.SetupPSBT, globalConfig.Network)
            return {
                OfferDomainModel.ContractInfo = c
                LockTime = lockTime
                RefundLockTime = rLockTime
                EventFullName = e
                LocalName = localName
                FeeRate = f
                PSBT = psbt
            }
        }
        
    member this.HasError(n): bool =
        let v = this.Validate(n)
        match v.ContractInfoErr, v.LocalNameErr, v.LockTimeErr, v.RefundLockTimeErr, v.EventFullNameErr, v.FeeRateErr, v.PSBTErr with
        | None, None, None, None, None, None, None -> false
        | _ -> true
        
type State =
    {
        OfferInEdit: OfferVM
        Error: string option
    }
    
type OfferUpdate =
    private
    | ContractInfoUpdate of string
    | LockTimeUpdate of string
    | RefundLockTimeUpdate of string
    | LocalNameUpdate of string
    | FeeRateUpdate of string
    | EventFullnameUpdate of string
    | PSBTUpdate of string
type InternalMsg =
    | NewOffer of NewOfferMetadata
    | UpdateOffer of OfferUpdate
    | CommitOffer
    | InvalidInput of msg: string
    | ResetErrorMsg
    | Reset
    
type OfferResult = {
    Msg: string
    OfferBase64: string
    OfferJson: string
}
type OutMsg =
    /// Represents 1. msg 2. Offer to send other 3. same offer in json
    | OfferAccepted of AsyncOperationStatus<OfferResult>
    
type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg
    
type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
    OnOfferAccepted: AsyncOperationStatus<OfferResult> -> 'Msg
}

type Translator<'Msg> = Msg -> 'Msg

let translator ({ OnInternalMsg = onInternalMsg; OnOfferAccepted = onOfferAccepted }: TranslationDictionary<'Msg>): Translator<'Msg> =
    function
    | ForSelf i -> onInternalMsg i
    | ForParent (OfferAccepted msg) -> onOfferAccepted msg
    
let init: State * Cmd<InternalMsg> =
    {
      OfferInEdit = OfferVM.Empty
      Error = None
      }, Cmd.none
    
    
[<AutoOpen>]
module private Tasks =
    let private fixCasing (evt: Repository.Event, payoffs: DiscretePayoffs) =
        result {
            let outcomes = HashSet<DiscreteOutcome>()
            for i in 0..(payoffs.Count - 1) do
                match payoffs.[i].Outcome.OutcomeString |> Option.ofObj with
                | None -> return! Error("The payoff cannot be parsed")
                | Some p ->
                    let knownOutcome = evt.Outcomes |> Seq.map(DiscreteOutcome) |> fun s -> s.FirstOrDefault(fun o -> o.OutcomeString.Equals(p, StringComparison.OrdinalIgnoreCase))
                    if (knownOutcome |> isNull) then return! Error(sprintf "The outcome %s is not part of the event" p) else
                    outcomes.Add(knownOutcome) |> ignore;
                    payoffs.[i] <- DiscretePayoff(knownOutcome, payoffs.[i].Reward)
            if (outcomes.Count <> evt.Outcomes.Length) then
                return! Error ("You did not specified the reward of all outcomes of the event")
        }
    /// Returns
    /// 1. base64-encoded Offer message to send other peer
    /// 2. string repr of that offer
    let tryCreateDLC globalConfig (o: OfferVM): Task<Result<(string * string), _>> = task {
        let d = o.ToDomainModel(globalConfig) |> function Error e -> failwithf "Unreachable state: %A.\n Error %s" o e | Ok r -> r
        let! maybeExistingDLC = CommandBase.tryGetDLC globalConfig o.LocalName
        match maybeExistingDLC with
        | Some _ ->
            return Error("DLC with the same name already exists!")
        | _ ->
        let! oracle = CommandBase.getOracle globalConfig (d.EventFullName.OracleName)
        let! evt = CommandBase.getEvent (globalConfig) (d.EventFullName)
        
        match fixCasing(evt, d.ContractInfo) with
        | Error e -> return Error e
        | Ok _ ->
        
        let builder = DLCTransactionBuilder(true, null, null, null, globalConfig.Network)
        
        let timeout =
            let t = Timeouts()
            t.ContractMaturity <- d.LockTime
            t.ContractTimeout <- d.RefundLockTime
            t
           
        builder.Offer(oracle.PubKey, evt.EventId.RValue, d.ContractInfo, timeout) |> ignore;
        let repo = ConfigUtils.repository globalConfig
        let! dlc = repo.NewDLC(evt.EventId, builder)
        let nameRepo = ConfigUtils.nameRepo globalConfig
        do! nameRepo.AsDLCNameRepository().SetMapping(o.LocalName, dlc.Id)
        
        // from setup command
        let! (keypath, key) = repo.CreatePrivateKey()
        let offer = builder.FundOffer(key, d.PSBT)
        offer.OffererContractId <- dlc.Id
        dlc.FundKeyPath <- keypath;
        dlc.Abort <- d.PSBT;
        dlc.BuilderState <- builder.ExportStateJObject();
        dlc.Offer <- JObject.FromObject(offer, JsonSerializer.Create(repo.JsonSettings))
        do! repo.SaveDLC(dlc)
        
        let jsonTxt = JsonConvert.SerializeObject(offer, repo.JsonSettings)
        let base64Txt = Encoders.Base64.EncodeData(UTF8Encoding.UTF8.GetBytes(jsonTxt))
        return Ok(base64Txt, jsonTxt)
    }

let rec update (globalConfig) (msg: InternalMsg) (state: State) =
    match msg with
    | Reset ->
        let s, cmd = init
        s, (cmd |> Cmd.map ForSelf)
    | NewOffer data ->
        let o = OfferVM.FromMetadata(data)
        {state with OfferInEdit = (o)}, Cmd.none
    | UpdateOffer update ->
        match update with
        | ContractInfoUpdate u ->
            { state with OfferInEdit = { state.OfferInEdit with ContractInfo = u }; }
        | LockTimeUpdate u ->
            { state with OfferInEdit = { state.OfferInEdit with LockTime = u }; }
        | RefundLockTimeUpdate u ->
            { state with OfferInEdit = { state.OfferInEdit with RefundLockTime = u }; }
        | LocalNameUpdate u ->
            { state with OfferInEdit = { state.OfferInEdit with LocalName = u }; }
        | FeeRateUpdate u ->
            { state with OfferInEdit = { state.OfferInEdit with FeeRate = u }; }
        | EventFullnameUpdate u ->
            { state with OfferInEdit = { state.OfferInEdit with EventFullName = u }; }
        | PSBTUpdate u ->
            { state with OfferInEdit = { state.OfferInEdit with SetupPSBT = u }; }
        ,Cmd.none
    | InvalidInput msg ->
        { state with Error = Some (msg)}, Cmd.none
    | ResetErrorMsg ->
        { state with Error = None }, Cmd.none
    | CommitOffer ->
         let job =
            Cmd.OfTask.either ((tryCreateDLC globalConfig) >> Task.map(function | Ok x -> x | Error e -> raise <| Exception (e.ToString())))
                              (state.OfferInEdit)
                              (fun (offer, offerJson) ->
                                Finished({ Msg = "Finished Creating New Offer"; OfferBase64 = offer; OfferJson = offerJson })
                                |> OfferAccepted |> ForParent)
                              (fun e -> e.Message |> InvalidInput |> ForSelf)
         state, Cmd.batch[Cmd.ofMsg (Started |> OfferAccepted |> ForParent); Cmd.ofMsg(ResetErrorMsg |> ForSelf); job]
    
let view globalConfig (state: State) (dispatch) =
    StackPanel.create [
        StackPanel.horizontalAlignment HorizontalAlignment.Center
        StackPanel.verticalAlignment VerticalAlignment.Center
        StackPanel.orientation Orientation.Vertical
        StackPanel.width 450.
        StackPanel.margin 10.
        StackPanel.children [
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "EventFullname"
                TextBox.watermark "Event full name: (<oracle/event>)"
                TextBox.text (state.OfferInEdit.EventFullName)
                TextBox.errors (state.OfferInEdit.Validate(globalConfig.Network).EventFullNameErr |> Option.toArray |> Seq.cast<obj>)
                yield! TextBox.onTextInput(
                    EventFullnameUpdate >>  UpdateOffer >> ForSelf >> dispatch
                    )
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "Payoff"
                TextBox.watermark "Payoffs. a.k.a contract_info: (outcome:reward,outcome:-loss)"
                TextBox.text (state.OfferInEdit.ContractInfo)
                TextBox.errors (state.OfferInEdit.Validate(globalConfig.Network).ContractInfoErr |> Option.toArray |> Seq.cast<obj>)
                yield! TextBox.onTextInput(
                    ContractInfoUpdate >>  UpdateOffer >> ForSelf >> dispatch
                    )
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "Localname"
                TextBox.watermark "Local Name For the offer"
                TextBox.text (state.OfferInEdit.LocalName)
                TextBox.errors (state.OfferInEdit.Validate(globalConfig.Network).LocalNameErr |> Option.toArray |> Seq.cast<obj>)
                yield! TextBox.onTextInput(
                    LocalNameUpdate >>  UpdateOffer >> ForSelf >> dispatch
                    )
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "Locktime"
                TextBox.watermark "CET locktime. a.k.a. 'contract_maturity_bound'"
                TextBox.text (state.OfferInEdit.LockTime)
                TextBox.errors (state.OfferInEdit.Validate(globalConfig.Network).LockTimeErr |> Option.toArray |> Seq.cast<obj>)
                yield! TextBox.onTextInput(
                    LockTimeUpdate >>  UpdateOffer >> ForSelf >> dispatch
                    )
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "RefundLocktime. a.k.a 'contract_timeout'"
                TextBox.watermark "Refund locktime"
                TextBox.text (state.OfferInEdit.RefundLockTime)
                TextBox.errors (state.OfferInEdit.Validate(globalConfig.Network).RefundLockTimeErr |> Option.toArray |> Seq.cast<obj>)
                yield! TextBox.onTextInput(
                    RefundLockTimeUpdate >>  UpdateOffer >> ForSelf >> dispatch
                    )
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "FeeRate"
                TextBox.watermark "Feerate"
                TextBox.text (state.OfferInEdit.FeeRate)
                TextBox.errors (state.OfferInEdit.Validate(globalConfig.Network).FeeRateErr |> Option.toArray |> Seq.cast<obj>)
                yield! TextBox.onTextInput(
                    FeeRateUpdate >>  UpdateOffer >> ForSelf >> dispatch
                    )
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "PSBT"
                let collateralMsg =
                    state.OfferInEdit.TryGetCollateral()
                    |> function
                        | Ok x -> sprintf "It must pay %d satoshis to yourself" x.Satoshi
                        | Error _ -> ""
                TextBox.watermark (sprintf "Paste your PSBT here! " + collateralMsg)
                TextBox.height 120.
                TextBox.text (state.OfferInEdit.SetupPSBT)
                TextBox.errors (state.OfferInEdit.Validate(globalConfig.Network).PSBTErr |> Option.toArray |> Seq.cast<obj>)
                yield! TextBox.onTextInput(
                    PSBTUpdate >>  UpdateOffer >> ForSelf >> dispatch
                    )
            ]
            StackPanel.create [
                StackPanel.dock Dock.Bottom
                StackPanel.orientation Orientation.Horizontal
                StackPanel.children [
                    Button.create[
                        Button.classes ["round"]
                        Button.content "Cancel"
                        Button.onClick(fun _ -> Reset |> ForSelf |> dispatch)
                    ]
                    Button.create [
                        Button.isEnabled (state.OfferInEdit.HasError(globalConfig.Network) |> not)
                        Button.classes ["round"]
                        Button.dock Dock.Right
                        Button.content "Ok"
                        Button.onClick(fun _ -> CommitOffer |> ForSelf |> dispatch)
                    ]
                ]
            ]
            match state.Error with
            | None -> ()
            | Some x ->
                TextBlock.create[
                    TextBlock.text x
                    TextBlock.classes ["error"]
                ]
        ]
    ]
    
