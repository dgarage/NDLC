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

open NDLC.GUI
open NDLC.GUI.GlobalMsgs
open NDLC.GUI.Utils

type OfferVM = {
    ContractInfo: ContractInfo seq
    /// The locktime of the CET transaction
    LockTime: LockTime
    RefundLockTime: LockTime
    EventFullName: EventFullName
    LocalName: string
}
    with
    static member Empty = {
        ContractInfo = Seq.empty
        LockTime = LockTime.Zero
        RefundLockTime = LockTime(4999999)
        EventFullName = EventFullName.Empty
        LocalName = ""
    }
    static member FromMetadata(m: NewOfferMetadata) = {
        ContractInfo = Seq.empty
        LockTime =  LockTime.Zero
        RefundLockTime = LockTime(4999999)
        EventFullName = m.EventFullName
        LocalName = ""
    }
    member this.ToOfferMsg() =
        task {
            let o = Offer()
            if (this.ContractInfo.Count() < 2) then return Error("You must specify at least 2 contract info!") else
            if (this.LocalName |> String.IsNullOrEmpty) then  return Error("You must specify LocalName") else
            o.ContractInfo <- this.ContractInfo |> Array.ofSeq
            o.Timeouts <-
                let t = Timeouts()
                t.ContractMaturity <- (this.LockTime)
                t.ContractTimeout <- (this.RefundLockTime)
                t
            return Ok o
        }
type State =
    {
        OfferInEdit: OfferVM
        Error: string option
    }
    
type InternalMsg =
    | NewOffer of NewOfferMetadata
    | UpdateOffer of OfferVM
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
    
    
let tryGetDLC globalConfig (dlcName: string) = task {
        return failwith ""
    }
let update (globalConfig) (msg: InternalMsg) (state: State) =
    match msg with
    | NewOffer data ->
        let o = OfferVM.FromMetadata(data)
        {state with OfferInEdit = (o)}, Cmd.none
    | UpdateOffer vm ->
        { state with OfferInEdit = (vm); Error = None }, Cmd.none
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
                TextBox.text (state.OfferInEdit.EventFullName.ToString())
                TextBox.errors (state.Error |> Option.toArray |> Seq.cast<obj>)
                yield! TextBox.onTextInput(fun s ->
                    match EventFullName.TryParse s with
                    | true, eventFullName ->
                        let newOffer = { state.OfferInEdit with EventFullName = eventFullName }
                        newOffer |> UpdateOffer |> ForSelf |> dispatch
                    | _ -> "Invalid EventFullName! (It must be in the form of 'oraclename/eventname'" |> InvalidInput |> ForSelf |> dispatch
                    )
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "Payoff"
                TextBox.watermark "Payoffs. a.k.a contract_info: (outcome:reward,outcome:-loss)"
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "Localname"
                TextBox.watermark "Local Name For the offer"
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "Locktime"
                TextBox.watermark "CET locktime. a.k.a. 'contract_maturity_bound'"
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "RefundLocktime. a.k.a 'contract_timeout'"
                TextBox.watermark "Refund locktime"
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "FeeRate"
                TextBox.watermark "Feerate"
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
                        Button.isEnabled state.Error.IsNone
                        Button.classes ["round"]
                        Button.dock Dock.Right
                        Button.content "Ok"
                        Button.onClick(fun _ -> CommitOffer |> ForSelf |> dispatch)
                    ]
                ]
            ]
        ]
    ]
    
