namespace NDLC.GUI

open Avalonia
open FSharp.Control.Tasks
open Avalonia.FuncUI.DSL
open Elmish
open Avalonia.Controls
open Avalonia.Media
open GlobalMsgs
open NBitcoin
open NDLC.GUI.Utils
open NDLC.Infrastructure

 module DLCModule =
    type FromChild =
        | OfferResult of DLCOfferModule.OfferResult
        | AcceptResult of DLCAcceptModule.AcceptResult
        | CheckSigsResult of DLCCheckSigsModule.CheckSigResult
        | StartResult of DLCStartModule.StartResult
        | ExecResult of Transaction
        
    type Page =
        | Offer = 0
        | Accept = 1
        | CheckSigs = 2
        | Start = 3
        | List = 4
    
    type State = {
        List: DLCListModule.State
        Offer: DLCOfferModule.State
        Accept: DLCAcceptModule.State
        Checksigs: DLCCheckSigsModule.State
        Start: DLCStartModule.State
        OutputToShowUser: Deferred<FromChild>
        SelectedIndex: Page
    }
    
    type Msg =
        | ListMsg of DLCListModule.InternalMsg
        | OfferMsg of DLCOfferModule.InternalMsg
        | AcceptMsg of DLCAcceptModule.InternalMsg
        | CheckSigsMsg of DLCCheckSigsModule.InternalMsg
        | StartMsg of DLCStartModule.InternalMsg
        
        | OutputReturned of FromChild
        | CopyToClipBoard of string
        | NavigateTo of Page
        | NoOp
        | Sequence of Msg seq
        
    let listTranslator globalConfig =
        DLCListModule.translator {
            OnInternalMsg = ListMsg
            OnGoToNextStep = fun info ->
                let isInit = info.IsInitiator
                match info.DLCState.GetNextStep(globalConfig.Network) with
                | Repository.DLCState.DLCNextStep.Setup when isInit ->
                    Sequence([ DLCOfferModule.Reset |> OfferMsg
                               { NewOfferMetadata.EventFullName = info.KnownDLC.EventName; Outcomes = info.KnownDLC.Outcomes }
                               |> DLCOfferModule.NewOffer
                               |> OfferMsg
                               (DLCOfferModule.LocalNameUpdate(info.LocalName))|> DLCOfferModule.UpdateOffer |> OfferMsg;
                               NavigateTo(Page.Offer)])
                | Repository.DLCState.DLCNextStep.Setup ->
                    Sequence([ DLCAcceptModule.Reset |> AcceptMsg
                               NavigateTo(Page.Accept)])
                | Repository.DLCState.DLCNextStep.CheckSigs when isInit ->
                    Sequence([ DLCCheckSigsModule.Reset |> CheckSigsMsg
                               NavigateTo(Page.Start)])
                | Repository.DLCState.DLCNextStep.CheckSigs ->
                    Sequence([ DLCAcceptModule.Reset |> AcceptMsg
                               NavigateTo(Page.Accept)])
                | Repository.DLCState.DLCNextStep.Fund when isInit ->
                    Sequence([ DLCStartModule.Reset |> StartMsg
                               DLCStartModule.UpdateLocalName info.LocalName |> StartMsg
                               NavigateTo(Page.Offer)])
                | Repository.DLCState.DLCNextStep.Fund ->
                    Sequence([ DLCAcceptModule.Reset |> AcceptMsg
                               NavigateTo(Page.Accept)])
                | Repository.DLCState.DLCNextStep.Done ->
                    NoOp
                | ns -> failwithf "Unreachable! Unknown nextStep %A" (ns)
            OnSetExecutionResult = ExecResult >> OutputReturned
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
        
    let checkSigsTranslator =
        DLCCheckSigsModule.translator {
            OnInternalMsg = CheckSigsMsg
            OnFinished = fun x ->Sequence[ CheckSigsResult x |> OutputReturned; DLCCheckSigsModule.Reset |> CheckSigsMsg]
        }
        
    let startTranslator =
        DLCStartModule.translator {
            OnInternalMsg = StartMsg
            OnFinished = fun x ->Sequence[ StartResult x |> OutputReturned; DLCStartModule.Reset |> StartMsg]
        }
            
    let init =
        let o, oCmd = DLCOfferModule.init
        let a = DLCAcceptModule.init
        let dlcList, listCmd = DLCListModule.init
        {
            List = dlcList
            Offer = o
            Accept = a
            Checksigs = DLCCheckSigsModule.init
            Start = DLCStartModule.init
            OutputToShowUser = HasNotStartedYet
            SelectedIndex = Page.Offer
        }, Cmd.batch([ oCmd |> Cmd.map(OfferMsg); listCmd |> Cmd.map(ListMsg) ])
    let rec update globalConfig msg state =
        match msg with
        | ListMsg msg ->
            let s, cmd = DLCListModule.update globalConfig msg state.List
            { state with List = s }, (cmd |> Cmd.map(listTranslator globalConfig))
        | OfferMsg msg ->
            let s, cmd = DLCOfferModule.update globalConfig msg state.Offer 
            {  state with Offer = s }, (cmd |> Cmd.map (offerTranslator))
        | AcceptMsg msg ->
            let s, cmd = DLCAcceptModule.update globalConfig msg state.Accept
            {  state with Accept = s }, (cmd |> Cmd.map (acceptTranslator))
        | CheckSigsMsg msg ->
            let s, cmd = DLCCheckSigsModule.update globalConfig msg state.Checksigs
            { state with Checksigs = s }, (cmd |> Cmd.map(checkSigsTranslator))
        | StartMsg msg ->
            let s, cmd = DLCStartModule.update globalConfig msg state.Start
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
                            TabItem.onTapped(fun _ ->  NavigateTo Page.Offer |> dispatch)
                        ]
                        
                        TabItem.create [
                            TabItem.header "Accept"
                            TabItem.content (DLCAcceptModule.view globalConfig state.Accept (acceptTranslator >> dispatch))
                            TabItem.onTapped(fun _ -> NavigateTo Page.Accept |> dispatch)
                        ]
                        
                        TabItem.create [
                            TabItem.header "CheckSigs"
                            TabItem.content (DLCCheckSigsModule.view globalConfig state.Checksigs (checkSigsTranslator >> dispatch))
                            TabItem.onTapped(fun _ -> NavigateTo Page.CheckSigs |> dispatch)
                        ]
                        TabItem.create [
                            TabItem.header "Start"
                            TabItem.content (DLCStartModule.view globalConfig state.Start (startTranslator >> dispatch))
                            TabItem.onTapped(fun _ -> NavigateTo Page.Start |> dispatch)
                        ]
                        TabItem.create [
                            TabItem.header "List"
                            TabItem.content (DLCListModule.view globalConfig state.List (listTranslator globalConfig >> dispatch))
                            TabItem.onTapped(fun _ -> Sequence[DLCListModule.LoadDLCs(Started) |> ListMsg; NavigateTo(Page.List); ] |> dispatch)
                        ]
                    ]
                ]
                
                let resultView (msg, base64, json) =
                    StackPanel.children [
                        TextBlock.create [
                            TextBlock.fontSize 14.
                            TextBlock.text (msg)
                        ]
                        TextBlock.create [
                            TextBlock.text (base64)
                            TextBlock.margin 10.
                            TextBlock.textWrapping TextWrapping.Wrap
                            TextBlock.height 200.
                            TextBlock.contextMenu (ContextMenu.create [
                                ContextMenu.viewItems [
                                    MenuItem.create [
                                        MenuItem.header "Copy"
                                        MenuItem.onClick(fun (_) -> base64 |> CopyToClipBoard |> dispatch)
                                    ]
                                ]
                            ])
                        ]
                        TextBox.create [
                            TextBox.text (json)
                            TextBlock.margin 10.
                            TextBox.textWrapping TextWrapping.Wrap
                            TextBox.height 200.
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
                    | Resolved (CheckSigsResult x) ->
                        resultView(x.Msg, x.ExtractedPSBT.ToBase64(), x.ToString())
                    | Resolved (StartResult(DLCStartModule.ForInitiator x)) ->
                        resultView(x.Msg, x.SignBase64, x.SignJson)
                    | Resolved (StartResult(DLCStartModule.ForAcceptor x)) ->
                        resultView(x.Msg, x.FinalizedTxHex, x.FinalizedTxJson)
                    | Resolved (ExecResult t) ->
                        resultView("Broadcast this CET transaction", t.ToHex(), "")
                    | InProgress -> StackPanel.children [Components.spinner]
                    | HasNotStartedYet -> ()
                ]
            ]
        ]
