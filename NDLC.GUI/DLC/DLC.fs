namespace NDLC.GUI

open Avalonia
open FSharp.Control.Tasks
open Avalonia.FuncUI.DSL
open Elmish
open Avalonia.Controls
open NDLC.GUI.Utils
open NDLC.Infrastructure

 module DLCModule =
    type FromChild =
        | OfferResult of DLCOfferModule.OfferResult
        | AcceptResult of DLCAcceptModule.AcceptResult
        | StartResult of DLCSignModule.SignResult
        
    type Page =
        | Offer = 0
        | Accept = 1
        | Start = 2
        | List = 3
    
    type State = {
        List: DLCListModule.State
        Offer: DLCOfferModule.State
        Accept: DLCAcceptModule.State
        Start: DLCSignModule.State
        OutputToShowUser: Deferred<FromChild>
        SelectedIndex: Page
    }
    
    type Msg =
        | ListMsg of DLCListModule.InternalMsg
        | OfferMsg of DLCOfferModule.InternalMsg
        | AcceptMsg of DLCAcceptModule.InternalMsg
        | StartMsg of DLCSignModule.InternalMsg
        
        | OutputReturned of FromChild
        | CopyToClipBoard of string
        | NavigateTo of Page
        | NoOp
        | Sequence of Msg seq
        
    let listTranslator =
        DLCListModule.translator {
            OnInternalMsg = ListMsg
            OnGoToNextStep = fun { NextStep = ns; LocalName = n; IsInitiator = isInit } ->
                match ns with
                | Repository.DLCState.DLCNextStep.Setup when isInit ->
                    Sequence([ DLCOfferModule.Reset |> OfferMsg; NavigateTo(Page.Offer)])
                | Repository.DLCState.DLCNextStep.Setup ->
                    Sequence([ DLCAcceptModule.Reset |> AcceptMsg; NavigateTo(Page.Accept)])
                | Repository.DLCState.DLCNextStep.CheckSigs when isInit ->
                    Sequence([ DLCSignModule.Reset |> StartMsg; NavigateTo(Page.Start)])
                | Repository.DLCState.DLCNextStep.CheckSigs ->
                    Sequence([ DLCAcceptModule.Reset |> AcceptMsg; NavigateTo(Page.Accept)])
                | Repository.DLCState.DLCNextStep.Fund when isInit ->
                    Sequence([ DLCOfferModule.Reset |> OfferMsg; NavigateTo(Page.Offer)])
                | Repository.DLCState.DLCNextStep.Fund ->
                    Sequence([ DLCAcceptModule.Reset |> AcceptMsg; NavigateTo(Page.Accept)])
                | Repository.DLCState.DLCNextStep.Done ->
                    NoOp
                | _ -> failwithf "Unreachable! Unknown nextStep %A" (ns)
        }
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
        let dlcList, listCmd = DLCListModule.init
        {
            List = dlcList
            Offer = o
            Accept = a
            Start = DLCSignModule.init
            OutputToShowUser = HasNotStartedYet
            SelectedIndex = Page.Offer
        }, Cmd.batch([ oCmd |> Cmd.map(OfferMsg); listCmd |> Cmd.map(ListMsg) ])
    let rec update globalConfig msg state =
        match msg with
        | ListMsg msg ->
            let s, cmd = DLCListModule.update globalConfig msg state.List
            { state with List = s }, (cmd |> Cmd.map(ListMsg))
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
        | NavigateTo p ->
            { state with SelectedIndex = p }, Cmd.none
        | NoOp ->
            state, Cmd.none
        | Sequence msgs ->
           let folder (s, c) msg =
                let s', cmd = update globalConfig msg s
                s', cmd :: c
           let newState, cmdList = msgs |> Seq.fold(folder) (state, [])
           newState, (cmdList |> Cmd.batch)
    let view globalConfig (state: State) dispatch =
        DockPanel.create [
            DockPanel.children [
                TabControl.create [
                    TabControl.tabStripPlacement Dock.Left
                    TabControl.selectedIndex ((int)state.SelectedIndex)
                    TabControl.viewItems [
                        TabItem.create [
                            TabItem.header "Offer"
                            TabItem.content (DLCOfferModule.view globalConfig state.Offer (offerTranslator >> dispatch))
                            TabItem.onTapped(fun _ -> Page.Offer |> NavigateTo |> dispatch)
                        ]
                        
                        TabItem.create [
                            TabItem.header "Accept"
                            TabItem.content (DLCAcceptModule.view globalConfig state.Accept (acceptTranslator >> dispatch))
                            TabItem.onTapped(fun _ -> Page.Accept |> NavigateTo |> dispatch)
                        ]
                        
                        TabItem.create [
                            TabItem.header "Start"
                            TabItem.content (DLCSignModule.view globalConfig state.Start (startTranslator >> dispatch))
                            TabItem.onTapped(fun _ -> Page.Start |> NavigateTo |> dispatch)
                        ]
                        TabItem.create [
                            TabItem.header "List"
                            TabItem.content (DLCListModule.view globalConfig state.List (listTranslator >> dispatch))
                            TabItem.onTapped(fun _ -> Sequence[Page.List |> NavigateTo; DLCListModule.LoadDLCs(Started) |> ListMsg] |> dispatch)
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
