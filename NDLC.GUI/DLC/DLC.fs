namespace NDLC.GUI

open Avalonia
open FSharp.Control.Tasks
open Avalonia.FuncUI.DSL
open Elmish
open Avalonia.Controls
open NDLC.GUI.Utils

 module DLCModule =
    type FromChild =
        | OfferResult of DLCOfferModule.OfferResult
        | AcceptResult of DLCAcceptModule.AcceptResult
        | StartResult of DLCSignModule.SignResult
    type State = {
        Offer: DLCOfferModule.State
        Accept: DLCAcceptModule.State
        Start: DLCSignModule.State
        OutputToShowUser: Deferred<FromChild>
    }
    
    type Msg =
        | OfferMsg of DLCOfferModule.InternalMsg
        | AcceptMsg of DLCAcceptModule.InternalMsg
        | StartMsg of DLCSignModule.InternalMsg
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
        
    let startTranslator =
        DLCSignModule.translator {
            OnInternalMsg = StartMsg
            OnInputFinished = StartResult >> OutputReturned
        }
            
    let init =
        let o, oCmd = DLCOfferModule.init
        let a = DLCAcceptModule.init
        {
            Offer = o
            Accept = a
            Start = DLCSignModule.init
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
        | StartMsg msg ->
            let s, cmd = DLCSignModule.update globalConfig msg state.Start
            { state with Start = s }, (cmd |> Cmd.map(startTranslator))
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
                        
                        TabItem.create [
                            TabItem.header "Sign"
                            TabItem.content (DLCSignModule.view globalConfig state.Start (startTranslator >> dispatch))
                        ]
                    ]
                ]
                
                let resultView (msg, base64, json) =
                    StackPanel.children [
                        TextBlock.create [
                            TextBlock.text (msg)
                        ]
                        TextBox.create [
                            TextBox.text (base64)
                            TextBlock.contextMenu (ContextMenu.create [
                                ContextMenu.viewItems [
                                    MenuItem.create [
                                        MenuItem.header "Copy Base64"
                                        MenuItem.onClick(fun (_) -> base64 |> CopyToClipBoard |> dispatch)
                                    ]
                                ]
                            ])
                        ]
                        TextBox.create [
                            TextBox.text (json)
                            TextBlock.contextMenu (ContextMenu.create [
                                ContextMenu.viewItems [
                                    MenuItem.create [
                                        MenuItem.header "Copy Json"
                                        MenuItem.onClick(fun (_) -> json |> CopyToClipBoard |> dispatch)
                                    ]
                                ]
                            ])
                        ]
                    ]
                
                StackPanel.create [
                    StackPanel.isVisible (state.OutputToShowUser |> Deferred.hasNotStarted |> not)
                    match state.OutputToShowUser with
                    | Resolved (OfferResult x) ->
                        resultView(x.Msg, x.OfferBase64, x.OfferJson)
                    | Resolved (AcceptResult x) ->
                        resultView(x.Msg, x.AcceptBase64, x.AcceptJson)
                    | _ -> ()
                ]
            ]
        ]
