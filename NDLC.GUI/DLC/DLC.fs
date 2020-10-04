namespace NDLC.GUI

open Avalonia.FuncUI.DSL
open Elmish
open Avalonia.Controls
open NDLC.GUI.DLCOfferModule
open NDLC.GUI.Utils

 module DLCModule =
    type FromChild =
        | OfferResult of DLCOfferModule.OfferResult
    type State = {
        Offer: DLCOfferModule.State
        Accept: DLCAcceptModule.State
        OutputToShowUser: Deferred<FromChild>
    }
    
    type Msg =
        | OfferMsg of DLCOfferModule.InternalMsg
        | OutputReturned of FromChild
        | NoOp
        
    let offerTranslator =
        DLCOfferModule.translator
            {
                OnInternalMsg = OfferMsg
                OnOfferAccepted =
                    function
                        | Started -> NoOp
                        | Finished s ->
                            s |> OfferResult |> OutputReturned
            }
            
    let init =
        let o, oCmd = DLCOfferModule.init
        let a = DLCAcceptModule.init
        {
            Offer = o
            Accept = a
            OutputToShowUser = HasNotStartedYet
        }, Cmd.batch([ oCmd |> Cmd.map(OfferMsg); ])
    let update globalConfig msg state =
        match msg with
        | OfferMsg msg ->
            let s, cmd = DLCOfferModule.update globalConfig msg state.Offer 
            {  state with Offer = s }, (cmd |> Cmd.map (offerTranslator))
        | OutputReturned o ->
            { state with OutputToShowUser = Deferred.Resolved(o)}, Cmd.none
        | NoOp ->
            state, Cmd.none
    let view globalConfig (state: State) dispatch =
        DockPanel.create [
            DockPanel.children [
                TabControl.create [
                    TabControl.tabStripPlacement Dock.Left
                    TabControl.viewItems [
                        TabItem.create [
                            TabItem.header "Offer"
                            TabItem.content (DLCOfferModule.view globalConfig state.Offer (offerTranslator >> dispatch))
                        ]
                        
                        TabItem.create [
                            TabItem.header "Accept"
                        ]
                    ]
                ]
                
                StackPanel.create [
                    StackPanel.isVisible (state.OutputToShowUser |> Deferred.hasNotStarted |> not)
                    match state.OutputToShowUser with
                    | Resolved (OfferResult x) ->
                        StackPanel.children [
                            TextBlock.create [
                                TextBlock.text (x.Msg)
                            ]
                            TextBox.create [
                                TextBox.text (x.Offer.ToString())
                            ]
                            TextBox.create [
                                TextBox.text (x.OfferJson)
                            ]
                        ]
                    | _ -> ()
                ]
            ]
        ]
