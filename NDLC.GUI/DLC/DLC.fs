namespace NDLC.GUI

open Avalonia.FuncUI.DSL
open Elmish
open Avalonia.Controls
open NDLC.GUI.DLCOfferModule
open NDLC.GUI.Utils

 module DLCModule =
    type State = {
        Offer: DLCOfferModule.State
        Setup: DLCSetupModule.State
        Accept: DLCAcceptModule.State
        OutputToShowUser: Deferred<(string * string)>
    }
    
    type Msg =
        | OfferMsg of DLCOfferModule.InternalMsg
        | SetupMsg of DLCSetupModule.InternalMsg
        | OutputReturned of msg: string * content: string
        
    let offerTranslator =
        DLCOfferModule.translator
            {
                OnInternalMsg = OfferMsg
                OnOfferAccepted = fun x -> (sprintf "Finished creating Offer! next step is", x.ToString()) |> OutputReturned
            }
    let setupTranslator =
        DLCSetupModule.translator
            { DLCSetupModule.TranslationDictionary.OnInternalMsg = SetupMsg
              OnSetupFinished = fun x -> (sprintf "", x.ToString()) |> OutputReturned
            }
            
    let init =
        let o, oCmd = DLCOfferModule.init
        let s, sCmd = DLCSetupModule.init
        let a = DLCAcceptModule.init
        {
            Offer = o
            Setup = s
            Accept = a
            OutputToShowUser = HasNotStartedYet
        }, Cmd.batch([ oCmd |> Cmd.map(OfferMsg); sCmd |> Cmd.map(SetupMsg) ])
    let update globalConfig msg state =
        match msg with
        | OfferMsg msg ->
            let s, cmd = DLCOfferModule.update globalConfig msg state.Offer 
            {  state with Offer = s }, (cmd |> Cmd.map (offerTranslator))
        | SetupMsg msg ->
            let s, cmd = DLCSetupModule.update msg state.Setup
            { state with Setup = s }, (cmd |> Cmd.map SetupMsg)
        | OutputReturned (msg, content) ->
            { state with OutputToShowUser = Deferred.Resolved(msg, content)}, Cmd.none
    
    let view globalConfig (state: State) dispatch =
        DockPanel.create [
            DockPanel.children [
                TabControl.create [
                    TabControl.tabStripPlacement Dock.Left
                    TabControl.viewItems [
                        TabItem.create [
                            TabItem.header "Offer"
                            TabItem.content (DLCOfferModule.view state.Offer (offerTranslator >> dispatch))
                        ]
                        
                        TabItem.create [
                            TabItem.header "Setup"
                            TabItem.content (DLCSetupModule.view globalConfig state.Setup (setupTranslator >> dispatch))
                        ]
                        
                        TabItem.create [
                            TabItem.header "Accept"
                        ]
                    ]
                ]
                
                StackPanel.create [
                    StackPanel.isVisible (state.OutputToShowUser |> Deferred.hasNotStarted |> not)
                    match state.OutputToShowUser with
                    | Resolved (msg, content) ->
                        StackPanel.children [
                            TextBlock.create [
                                TextBlock.text msg
                            ]
                            TextBox.create [
                                TextBox.text content
                            ]
                        ]
                    | _ -> ()
                ]
            ]
        ]
