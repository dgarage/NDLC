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

open NBitcoin
open System.Threading.Tasks
open NDLC
open NDLC.GUI
open NDLC.GUI.GlobalMsgs
open NDLC.GUI.Utils

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
        

type ErrorState = private {
    ContractInfoErr: string option
    /// The locktime of the CET transaction
    LockTimeErr: string option
    RefundLockTimeErr: string option
    EventFullNameErr: string option
    LocalNameErr: string option
    FeeRateErr: string option
}

type OfferDomainModel = {
    ContractInfo: DiscretePayoffs
    /// The locktime of the CET transaction
    LockTime: LockTime
    RefundLockTime: LockTime
    EventFullName: EventFullName
    LocalName: string
    FeeRate: FeeRate
}

type OfferVM = {
    ContractInfo: string
    /// The locktime of the CET transaction
    LockTime: string
    RefundLockTime: string
    EventFullName: string
    LocalName: string
    FeeRate: string
}
    with
    static member Empty = {
        ContractInfo = ""
        LockTime = ""
        RefundLockTime = ""
        EventFullName = ""
        LocalName = ""
        FeeRate = ""
    }
    static member FromMetadata(m: NewOfferMetadata) = {
        ContractInfo = ""
        LockTime =  ""
        RefundLockTime = ""
        EventFullName = m.EventFullName.ToString()
        LocalName = ""
        FeeRate = ""
    }
    
    member this.Validate(): ErrorState =
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
        }
        
    member this.ToDomainModel() =
        result {
            let! c =  tryParseContractInfo this.ContractInfo
            let! localName = if (this.LocalName |> String.IsNullOrEmpty) then Error("You must specify LocalName") else Ok(this.LocalName)
            let! lockTime = tryParseLockTime this.LockTime
            let! rLockTime = tryParseLockTime this.RefundLockTime
            let! f = tryParseFeeRate this.FeeRate
            let! e = tryParseEventFullname this.FeeRate
            return {
                OfferDomainModel.ContractInfo = c
                LockTime = lockTime
                RefundLockTime = rLockTime
                EventFullName = e
                LocalName = localName
                FeeRate = f
            }
        }
        
    member this.HasError: bool =
        let v = this.Validate()
        match v.ContractInfoErr, v.LocalNameErr, v.LockTimeErr, v.RefundLockTimeErr, v.EventFullNameErr with
        | None, None, None, None, None -> false
        | _ -> true
        
    member this.ToOfferMsg() =
        task {
            let o = Offer()
            if (this.ContractInfo.Count() < 2) then return Error("You must specify at least 2 contract info!") else
            if (this.LocalName |> String.IsNullOrEmpty) then  return Error("You must specify LocalName") else
            return Ok o
        }
type State =
    {
        OfferInEdit: OfferVM
        Error: string option
    }
    
type OfferUpdate =
    | ContractInfoUpdate of string
    | LockTimeUpdate of string
    | RefundLockTimeUpdate of string
    | LocalNameUpdate of string
    | FeeRateUpdate of string
    | EventFullnameUpdate of string
type InternalMsg =
    | NewOffer of NewOfferMetadata
    | UpdateOffer of OfferUpdate
    | CommitOffer
    | InvalidInput of msg: string
    
type OutMsg =
    | OfferAccepted of AsyncOperationStatus<string * Offer>
    
type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg
    
type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
    OnOfferAccepted: AsyncOperationStatus<string * Offer> -> 'Msg
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
    let tryGetDLC globalConfig (dlcName: string) = task {
        return failwith ""
    }
    
    let getEvent(): Task<Repository.Event> = task {
        return failwith ""
    }

    let tryCreateDLC globalConfig (o: OfferVM) = task {
        let d = o.ToDomainModel() |> function Error e -> failwithf "Unreachable state: %A. Error %s" o e | Ok r -> r
        let! maybeExistingDLC = tryGetDLC globalConfig o.LocalName
        match maybeExistingDLC with
        | Some _ ->
            return Error("DLC with the same name already exists!")
        | _ ->
        let builder = DLCTransactionBuilder(true, null, null, null, globalConfig.Network)
        
        let! evt = getEvent()
        let! dlc = (ConfigUtils.repository globalConfig).NewDLC(evt.EventId, builder)
        let nameRepo = ConfigUtils.nameRepo globalConfig
        do! nameRepo.AsDLCNameRepository().SetMapping(o.LocalName, dlc.Id)
        return Ok()
    }

let update (globalConfig) (msg: InternalMsg) (state: State) =
    match msg with
    | NewOffer data ->
        let o = OfferVM.FromMetadata(data)
        {state with OfferInEdit = (o)}, Cmd.none
    | UpdateOffer update ->
        match update with
        | ContractInfoUpdate u ->
            { state with OfferInEdit = { state.OfferInEdit with ContractInfo = u }; }, Cmd.none
        | LockTimeUpdate u ->
            { state with OfferInEdit = { state.OfferInEdit with LockTime = u }; }, Cmd.none
        | RefundLockTimeUpdate u ->
            { state with OfferInEdit = { state.OfferInEdit with RefundLockTime = u }; }, Cmd.none
        | LocalNameUpdate u ->
            { state with OfferInEdit = { state.OfferInEdit with LocalName = u }; }, Cmd.none
        | FeeRateUpdate u ->
            { state with OfferInEdit = { state.OfferInEdit with FeeRate = u }; }, Cmd.none
        | EventFullnameUpdate u ->
            { state with OfferInEdit = { state.OfferInEdit with EventFullName = u }; }, Cmd.none
    | InvalidInput msg ->
        { state with Error = Some (msg)}, Cmd.none
    | CommitOffer ->
         let job =
            Cmd.OfTask.either (state.OfferInEdit.ToOfferMsg >> Task.map(function | Ok x -> x | Error e -> raise <| Exception (e.ToString())))
                              ()
                              (fun x -> Finished("Finished Creating New Offer", x) |> OfferAccepted |> ForParent)
                              (fun e -> e.ToString() |> InvalidInput |> ForSelf)
         state, Cmd.batch[Cmd.ofMsg (Started |> OfferAccepted |> ForParent); job]
    
let view (state: State) (dispatch) =
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
                TextBox.errors (state.OfferInEdit.Validate().EventFullNameErr |> Option.toArray |> Seq.cast<obj>)
                yield! TextBox.onTextInput(fun s ->
                    s |> EventFullnameUpdate |>  UpdateOffer |> ForSelf |> dispatch
                    )
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "Payoff"
                TextBox.watermark "Payoffs. a.k.a contract_info: (outcome:reward,outcome:-loss)"
                TextBox.text (state.OfferInEdit.ContractInfo)
                TextBox.errors (state.OfferInEdit.Validate().ContractInfoErr |> Option.toArray |> Seq.cast<obj>)
                yield! TextBox.onTextInput(fun s ->
                    s |> ContractInfoUpdate |>  UpdateOffer |> ForSelf |> dispatch
                    )
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "Localname"
                TextBox.watermark "Local Name For the offer"
                TextBox.errors (state.OfferInEdit.Validate().LocalNameErr |> Option.toArray |> Seq.cast<obj>)
                yield! TextBox.onTextInput(fun s ->
                    s |> LocalNameUpdate |>  UpdateOffer |> ForSelf |> dispatch
                    )
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "Locktime"
                TextBox.watermark "CET locktime. a.k.a. 'contract_maturity_bound'"
                TextBox.errors (state.OfferInEdit.Validate().LockTimeErr |> Option.toArray |> Seq.cast<obj>)
                yield! TextBox.onTextInput(fun s ->
                    s |> LockTimeUpdate |>  UpdateOffer |> ForSelf |> dispatch
                    )
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "RefundLocktime. a.k.a 'contract_timeout'"
                TextBox.watermark "Refund locktime"
                TextBox.errors (state.OfferInEdit.Validate().RefundLockTimeErr |> Option.toArray |> Seq.cast<obj>)
                yield! TextBox.onTextInput(fun s ->
                    s |> RefundLockTimeUpdate |>  UpdateOffer |> ForSelf |> dispatch
                    )
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "FeeRate"
                TextBox.watermark "Feerate"
                TextBox.errors (state.OfferInEdit.Validate().FeeRateErr |> Option.toArray |> Seq.cast<obj>)
                yield! TextBox.onTextInput(fun s ->
                    s |> FeeRateUpdate |>  UpdateOffer |> ForSelf |> dispatch
                    )
            ]
            StackPanel.create [
                StackPanel.dock Dock.Bottom
                StackPanel.orientation Orientation.Horizontal
                StackPanel.children [
                    Button.create [
                        Button.classes ["round"]
                        Button.dock Dock.Right
                        Button.content "Cancel"
                    ]
                    Button.create [
                        Button.isEnabled (state.OfferInEdit.HasError |> not)
                        Button.classes ["round"]
                        Button.dock Dock.Right
                        Button.content "Ok"
                        Button.onClick(fun _ -> CommitOffer |> ForSelf |> dispatch)
                    ]
                ]
            ]
        ]
    ]
    
