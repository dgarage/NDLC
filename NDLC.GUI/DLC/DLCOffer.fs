module NDLC.GUI.DLCOfferModule

open System
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

open System.Threading.Tasks
open NDLC
open NDLC.GUI
open NDLC.GUI.GlobalMsgs
open NDLC.GUI.Utils
open Newtonsoft.Json
open Newtonsoft.Json.Linq

[<AutoOpen>]
module private Helpers =
    let tryParseContractInfo (str: string) =
        match DiscretePayoffs.TryParse(str) with
        | false, _ -> Error("Invalid payoffs!")
        | true, c ->
            if (c.Count < 2) then Error("You must specify at least 2 contract info!") else
            Ok(c)
            
    let validateContractInfo(str: string) =
        match tryParseContractInfo(str) with
        | Ok _ -> None
        | Error e -> Some e
        
    let tryParseLockTime (str: string) =
        match UInt32.TryParse str with
        | true, r -> Ok(LockTime(r))
        | _ -> Error("Invalid input for locktime")
        
    let validateLockime(str: string) =
        match tryParseLockTime(str) with
        | Ok _ -> None
        | Error e -> Some (e)
        
    let tryParseEventFullname (str: string) =
        match EventFullName.TryParse str with
        | true, r -> Ok(r)
        | _ -> Error("Invalid input for EventFullName")
        
    let validateEventFullName(str: string) =
        match tryParseEventFullname(str) with
        | Ok _ -> None
        | Error e -> Some (e)
        
    let tryParseFeeRate (str: string) =
        match Int64.TryParse str with
        | true, r -> Ok(FeeRate(Money (r)))
        | _ -> Error("Invalid input for FeeRate")
        
    let validateFeeRate(str: string) =
        match tryParseFeeRate(str) with
        | Ok _ -> None
        | Error e -> Some (e)
        
    let tryParsePSBT (str: string, n: Network)=
        match PSBT.TryParse(str, n) with
        | true, psbt -> Ok psbt
        | _ -> Error("Invalid PSBT! Maybe wrong Network?")

    let validatePSBT(str: string, n: Network) =
        match tryParsePSBT (str, n) with
        | Ok _ -> None
        | Error e -> Some (e)

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
        LockTime = ""
        RefundLockTime = ""
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
                if (this.LocalName |> String.IsNullOrEmpty) then Some("You must specify LocalName") else None
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
    
type OfferResult = {
    Msg: string
    Offer: Offer
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
    /// Returns
    /// 1. Offer message to send other peer
    /// 2. string repr of that offer
    let tryCreateDLC globalConfig (o: OfferVM): Task<Result<(Offer * string), _>> = task {
        let d = o.ToDomainModel(globalConfig) |> function Error e -> failwithf "Unreachable state: %A.\n Error %s" o e | Ok r -> r
        let! maybeExistingDLC = CommandBase.tryGetDLC globalConfig o.LocalName
        match maybeExistingDLC with
        | Some _ ->
            return Error("DLC with the same name already exists!")
        | _ ->
        let builder = DLCTransactionBuilder(true, null, null, null, globalConfig.Network)
        
        let! oracle = CommandBase.getOracle globalConfig (d.EventFullName.Name)
        let! evt = CommandBase.getEvent (globalConfig) (d.EventFullName)
        let timeout =
            let t = Timeouts()
            t.ContractMaturity <- LockTime 0
            t.ContractTimeout <- Constants.NeverLockTime
            t
           
        builder.Offer(oracle.PubKey, evt.EventId.RValue, d.ContractInfo, timeout) |> ignore;
        let! dlc = (ConfigUtils.repository globalConfig).NewDLC(evt.EventId, builder)
        let nameRepo = ConfigUtils.nameRepo globalConfig
        do! nameRepo.AsDLCNameRepository().SetMapping(o.LocalName, dlc.Id)
        
        // from setup command
        let repo = ConfigUtils.repository globalConfig
        let! (keypath, key) = repo.CreatePrivateKey()
        let offer = builder.FundOffer(key, d.PSBT)
        offer.OffererContractId <- dlc.Id
        dlc.FundKeyPath <- keypath;
        dlc.Abort <- d.PSBT;
        dlc.BuilderState <- builder.ExportStateJObject();
        dlc.Offer <- JObject.FromObject(offer, JsonSerializer.Create(repo.JsonSettings))
        do! repo.SaveDLC(dlc)
        
        let jsonTxt = JsonConvert.SerializeObject(obj, repo.JsonSettings);
        return Ok(offer, jsonTxt)
    }

let update (globalConfig) (msg: InternalMsg) (state: State) =
    match msg with
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
    | CommitOffer ->
         let job =
            Cmd.OfTask.either ((tryCreateDLC globalConfig) >> Task.map(function | Ok x -> x | Error e -> raise <| Exception (e.ToString())))
                              (state.OfferInEdit)
                              (fun (offer, offerJson) ->
                                Finished({ Msg = "Finished Creating New Offer"; Offer = offer; OfferJson = offerJson })
                                |> OfferAccepted |> ForParent)
                              (fun e -> e.ToString() |> InvalidInput |> ForSelf)
         state, Cmd.batch[Cmd.ofMsg (Started |> OfferAccepted |> ForParent); job]
    
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
                TextBox.errors (state.OfferInEdit.Validate(globalConfig.Network).PSBTErr |> Option.toArray |> Seq.cast<obj>)
                yield! TextBox.onTextInput(
                    PSBTUpdate >>  UpdateOffer >> ForSelf >> dispatch
                    )
            ]
            StackPanel.create [
                StackPanel.dock Dock.Bottom
                StackPanel.orientation Orientation.Horizontal
                StackPanel.children [
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
    
