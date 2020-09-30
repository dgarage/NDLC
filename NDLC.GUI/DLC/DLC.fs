namespace NDLC.GUI

open Avalonia.FuncUI.DSL
open Elmish
open Avalonia.Controls

module DLCAcceptModule =
    type State = {
        Nil: bool
    }
    
    let init =
        { Nil = false }
        
 module DLCModule =
    type State = {
        Offer: DLCOfferModule.State
        Setup: DLCSetupModule.State
        Accept: DLCAcceptModule.State
        OutputToShowUser: string option
    }
    
    type Msg =
        | OfferMsg of DLCOfferModule.InternalMsg
        | SetupMsg of DLCSetupModule.InternalMsg
        | OutputReturned of msg: string
        
    let offerTranslator =
        DLCOfferModule.translator
            {
                OnInternalMsg = OfferMsg
                OnOfferAccepted = OutputReturned
            }
    let setupTranslator =
        DLCSetupModule.translator
            { DLCSetupModule.TranslationDictionary.OnInternalMsg = SetupMsg
              OnSetupFinished = fun x -> x.ToString() |> OutputReturned
            }
            
    let init =
        let o, oCmd = DLCOfferModule.init
        let s, sCmd = DLCSetupModule.init
        let a = DLCAcceptModule.init
        {
            Offer = o
            Setup = s
            Accept = a
            OutputToShowUser = None
        }, Cmd.batch([ oCmd |> Cmd.map(OfferMsg); sCmd |> Cmd.map(SetupMsg) ])
    let update globalConfig state msg =
        match msg with
        | OfferMsg msg ->
            let s, cmd = DLCOfferModule.update globalConfig state.Offer msg
            {  state with Offer = s }, (cmd |> Cmd.map OfferMsg)
        | SetupMsg msg ->
            let s, cmd = DLCSetupModule.update state.Setup msg
            { state with Setup = s }, (cmd |> Cmd.map SetupMsg)
        | OutputReturned msg ->
            { state with OutputToShowUser = Some msg }, Cmd.none
    
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
            ]
        ]
