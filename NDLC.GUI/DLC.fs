namespace NDLC.GUI

open Avalonia.FuncUI.DSL
open Elmish
open NDLC
open NDLC.Infrastructure
open Avalonia.Controls

module DLCModule =
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
    type State =
        {
            DLCs: DLCInfo list
            EventFullName: string
        }
        
    let init =
        { DLCs = []
          EventFullName = "" }, Cmd.none
        
    type Msg =
        | Null
        
    let update (globalConfig) (msg: Msg) (state: State) =
        state, Cmd.none
        
    let view (state: State) dispatch =
        DockPanel.create [
            DockPanel.children [
                TabControl.create [
                    TabControl.tabStripPlacement Dock.Left
                    TabControl.viewItems [
                        TabItem.create [
                            TabItem.header "Offer"
                        ]
                        
                        TabItem.create [
                            TabItem.header "Setup"
                        ]
                        
                        TabItem.create [
                            TabItem.header "Accept"
                        ]
                    ]
                ]
            ]
        ]
