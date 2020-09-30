module NDLC.GUI.DLCOfferModule

open FSharp.Control.Tasks
open Avalonia.Controls
open Elmish
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open NBitcoin

open NDLC.Infrastructure
open NDLC.Messages

open NDLC.GUI.Utils
open NDLC.GUI
open NDLC.GUI.GlobalMsgs

type Outcome = {
    Name: string
    Odds: float
}
type MyRelationToDLC =
    | Offerer
    | Acceptor
type DLCNextStep =
    | Fund
    | CheckSigs
    | Setup
    | Done
    
type DLCInfo = {
    Name: string
    EventFullName: string
    LocalIdHex: string
    Role: MyRelationToDLC
    NextStep: DLCNextStep
    Outcomes: Outcome list
}
with
    member this.IsInitiator = this.Role = Offerer
    member this.NextStepExplanation =
        match this.NextStep with
        | DLCNextStep.Setup ->
            "You need to create the setup PSBT with your wallet sending {s.Us!.Collateral!.ToString(false, false)} BTC to yourself, it must not be broadcasted.
            The address receiving this amount will be the same address where the reward of the DLC will be received.
            Then your can use 'dlc setup {name} \"<PSBT>\"', and give this message to the other party."
        | DLCNextStep.CheckSigs when this.IsInitiator ->
            "You need to pass the offer to the other party, and the other party will need to accept by sending you back a signed message.
            Then you need to use `dlc checksigs \"<signed message>\"`.
            You can get the offer of this dlc with `dlc show --offer {name}`"
        | DLCNextStep.CheckSigs when not <| this.IsInitiator ->
            "You need to pass the accept message to the other party, and the other party needs to reply with a signed message.
            Then you need to use `dlc checksigs \"<signed message>\"`.
            You can get the accept message of this dlc with `dlc show --accept {name}`"
        | DLCNextStep.Fund when this.IsInitiator ->
            "You need to partially sign a PSBT funding the DLC. You can can get the PSBT with `dlc show --funding {name}`.
            Then you need to use `dlc start {name} \"<PSBT>\"` and send the signed message to the other party."
        | DLCNextStep.Fund when this.IsInitiator ->
            "You need to partially sign a PSBT funding the DLC. You can can get the PSBT with `dlc show --funding {name}`.
            Then you need to use `dlc start {name} \"<PSBT>\"` and broadcast the resulting transaction.";
        | DLCNextStep.Done when this.IsInitiator ->
            "Make sure the other party actually start the DLC by broadcasting the funding transaction.
            IF THE OTHER PARTY DOES NOT RESPOND and doesn't broadcast the funding in reasonable delay. YOU MUST ABORT this DLC by signing and broadcasting the abort transaction `dlc show --abort {name}`.
            The abort transaction spend the coins you used for your collateral back to yourself.
            This will prevent a malicious party to start the contract without your involvement when he knows the outcome.
            
            When the Oracle attests the event, you can settle this contract by running `dlc execute \"<attestation>\"` and broadcasting the transaction.
            
            If the Oracle never attests the event you can get a refund later by broadcasting `dlc show --refund \"{name}\"`.";
        | DLCNextStep.Done when not <| this.IsInitiator ->
            "You need to fully sign and broadcast the funding transaction. You can get the PSBT with `dlc show --funding`.
            When the Oracle attests the event, you can settle this contract by running `dlc execute \"<attestation>\"` and broadcasting the transaction.
            If the Oracle never attests the event you can get a refund later by broadcasting `dlc show --refund \"{name}\"`."
        | _ -> failwithf "Unreachable (%A): (%b)" this.NextStep this.IsInitiator

type OfferVM = {
    ContractInfo: ContractInfo seq
    /// The locktime of the CET transaction
    LockTime: LockTime option
    RefundLockTime: LockTime option
    EventFullName: EventFullName
}
    with
    static member Empty = {
        ContractInfo = Seq.empty
        LockTime = None
        RefundLockTime = None
        EventFullName = EventFullName.Empty
    }
    static member FromMetadata(m: NewOfferMetadata) = {
        ContractInfo = []
        LockTime = None
        RefundLockTime = None
        EventFullName = EventFullName.Empty
    }
    member this.ToOfferMsg() =
        let o = Offer()
        o.ContractInfo <- this.ContractInfo |> Array.ofSeq
        o.Timeouts <-
            let t = Timeouts()
            t.ContractMaturity <- Option.defaultValue (LockTime.Zero) (this.LockTime)
            t.ContractTimeout <- Option.defaultValue (LockTime(4999999)) (this.RefundLockTime)
            t
        o
type State =
    {
        DLCs: DLCInfo list
        Selected: DLCInfo option
        EventFullName: string
        OfferInEdit: OfferVM
        Error: string option
    }
    
type InternalMsg =
    | NewOffer of NewOfferMetadata
    | UpdateOffer of OfferVM
    | InvalidInput of msg: string
    
type OutMsg =
    | OfferAccepted of msg: string
    
type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg
    
type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
    OnOfferAccepted: string -> 'Msg
}

type Translator<'Msg> = Msg -> 'Msg

let translator ({ OnInternalMsg = onInternalMsg; OnOfferAccepted = onOfferAccepted }: TranslationDictionary<'Msg>): Translator<'Msg> =
    function
    | ForSelf i -> onInternalMsg i
    | ForParent (OfferAccepted msg) -> onOfferAccepted msg
    
let init: State * Cmd<InternalMsg> =
    { DLCs = []
      Selected = None
      EventFullName = ""
      OfferInEdit = OfferVM.Empty
      Error = None
      }, Cmd.none
    
    
let tryGetDLC globalConfig (dlcName: string) = task {
        return failwith ""
    }
let update (globalConfig) (state: State)(msg: InternalMsg)  =
    match msg with
    | NewOffer data ->
        let o = OfferVM.FromMetadata(data)
        {state with OfferInEdit = (o)}, Cmd.none
    | UpdateOffer vm ->
        { state with OfferInEdit = (vm); Error = None }, Cmd.none
    | InvalidInput msg ->
        { state with Error = Some (msg)}, Cmd.none
    
let view (state: State) (dispatch) =
    let state = state.OfferInEdit
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
                yield! TextBox.onTextInputFinished(fun s ->
                    match EventFullName.TryParse s with
                    | true, eventFullName ->
                        let s = { state with EventFullName = eventFullName }
                        s |> UpdateOffer |> ForSelf |> dispatch
                    | _ -> "Invalid EventFullName! (It must be in the form of 'oraclename/eventname'" |> InvalidInput |> ForSelf |> dispatch
                    )
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "Payoff"
                TextBox.watermark "Payoffs: (outcome:reward,outcome:-loss)"
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
                TextBox.watermark "CET locktime"
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.useFloatingWatermark true
                TextBox.name "RefundLocktime"
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
                        Button.classes ["round"]
                        Button.dock Dock.Right
                        Button.content "Ok"
                    ]
                ]
            ]
        ]
    ]
    
