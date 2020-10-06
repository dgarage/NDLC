namespace NDLC.GUI

open Avalonia
open FSharp.Control.Tasks
open Avalonia.FuncUI.DSL
open Elmish
open Avalonia.Controls
open NDLC.GUI.DLCOfferModule
open NDLC.GUI.Utils

 module DLCModule =
    type FromChild =
        | OfferResult of DLCOfferModule.OfferResult
        | AcceptResult of string
    type State = {
        Offer: DLCOfferModule.State
        Accept: DLCAcceptModule.State
        OutputToShowUser: Deferred<FromChild>
    }
    
    type Msg =
        | OfferMsg of DLCOfferModule.InternalMsg
        | AcceptMsg of DLCAcceptModule.InternalMsg
        | OutputReturned of FromChild
        | CopyToClipBoard of string
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
            
    let acceptTranslator =
        DLCAcceptModule.translator {
            OnInternalMsg = AcceptMsg
            OnInputFinished = AcceptResult >> OutputReturned
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
        | AcceptMsg msg ->
            let s, cmd = DLCAcceptModule.update globalConfig msg state.Accept
            {  state with Accept = s }, (cmd |> Cmd.map (acceptTranslator))
        | OutputReturned o ->
            { state with OutputToShowUser = Deferred.Resolved(o)}, Cmd.none
        | CopyToClipBoard x ->
            let copy (str) = task {
                do! Application.Current.Clipboard.SetTextAsync str
                return NoOp
            }
            state, Cmd.OfTask.result (copy x)
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
                            TabItem.content (DLCAcceptModule.view globalConfig state.Accept (acceptTranslator >> dispatch))
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
                                TextBox.text (x.OfferBase64)
                                TextBlock.contextMenu (ContextMenu.create [
                                    ContextMenu.viewItems [
                                        MenuItem.create [
                                            MenuItem.header "Copy Base64"
                                            MenuItem.onClick(fun (_) -> x.OfferBase64 |> CopyToClipBoard |> dispatch)
                                        ]
                                    ]
                                ])
                            ]
                            TextBox.create [
                                TextBox.text (x.OfferJson)
                                TextBlock.contextMenu (ContextMenu.create [
                                    ContextMenu.viewItems [
                                        MenuItem.create [
                                            MenuItem.header "Copy Json"
                                            MenuItem.onClick(fun (_) -> x.OfferJson |> CopyToClipBoard |> dispatch)
                                        ]
                                    ]
                                ])
                            ]
                        ]
                    | _ -> ()
                ]
            ]
        ]
