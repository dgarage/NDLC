[<RequireQualifiedAccess>]
module NDLC.GUI.DLCListModule

open System.Collections.Generic
open System.Diagnostics
open Avalonia
open Avalonia.Controls
open Avalonia.FuncUI.Components
open Avalonia.FuncUI.DSL
open Avalonia.Media
open System.Linq
open System.Text
open FSharp.Control.Tasks
open Elmish
open NBitcoin
open NBitcoin.DataEncoders
open NDLC.GUI
open NDLC.GUI.Utils
open NDLC.Infrastructure
open NDLC.Messages


type KnownDLC = {
    EventName: EventFullName
    LocalName: string
    Attestations: Dictionary<string, Key>
    State: (Repository.DLCState)
    Outcomes: string []
    IsInitiator: bool
}

type State = private {
    KnownDLCs: Deferred<Result<KnownDLC seq, string>>
    ErrorMsg: string option
}

type InternalMsg =
    | LoadDLCs of AsyncOperationStatus<Result<KnownDLC seq, string>>
    | CopyToClipBoard of string
    | Execute of KnownDLC
    | InvalidInput of string
    | NoOp
    
type GotoInfo = {
    IsInitiator: bool
    KnownDLC: KnownDLC
}
    with
    member this.LocalName = this.KnownDLC.LocalName
    member this.DLCState = this.KnownDLC.State
type OutMsg =
    | SetExecutionResult of CETTX: Transaction
    | GoToNextStep of GotoInfo
    
type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg
    
type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
    OnGoToNextStep: GotoInfo -> 'Msg
    OnSetExecutionResult: Transaction -> 'Msg
}

type Translator<'Msg> = Msg -> 'Msg

let translator ({ OnInternalMsg = onInternalMsg; OnGoToNextStep = onInputFinished; OnSetExecutionResult = onSetExecutionResult }: TranslationDictionary<'Msg>):
    Translator<'Msg> =
    function
    | ForSelf i -> onInternalMsg i
    | ForParent(GoToNextStep info) -> onInputFinished info
    | ForParent(SetExecutionResult r) -> onSetExecutionResult r
    
let init =
    { KnownDLCs = HasNotStartedYet; ErrorMsg = None }, Cmd.ofMsg(LoadDLCs Started)
    
let update (globalConfig) (msg: InternalMsg) (state: State) =
    match msg with
    | LoadDLCs(Started) ->
        let load (globalConfig) = task {
            let nRepo = ConfigUtils.nameRepo globalConfig
            let repo = ConfigUtils.repository globalConfig
            let! dlcs = nRepo.AsDLCNameRepository().ListDLCs()
            let result = ResizeArray()
            for (dlcName, dlcId) in dlcs do
                let! dState = repo.GetDLC(dlcId)
                let! eventFullName = nRepo.AsEventRepository().ResolveName(dState.OracleInfo)
                let! e = repo.GetEvent(dState.OracleInfo)
                let! event = CommandBase.getEvent globalConfig eventFullName
                
                let info =
                    let atts = event |> Option.ofObj |> Option.bind(fun e -> e.Attestations |> Option.ofObj) |> Option.defaultWith(fun () -> Dictionary<_,_>())
                    let builder = DLCTransactionBuilder(dState.BuilderState.ToString(), globalConfig.Network)
                    { LocalName = dlcName; State = dState; IsInitiator = builder.State.IsInitiator
                      EventName = eventFullName; Outcomes = e.Outcomes
                      Attestations = atts }
                result.Add(info)
            return result.OrderBy(fun x -> (x.EventName.ToString(), x.LocalName)) :> seq<_>
        }
        { state with KnownDLCs = InProgress },
        Cmd.OfTask.either (load) globalConfig
            (Ok >> Finished >> LoadDLCs >> ForSelf)
            (fun e -> e.ToString() |> Error |> Finished |> LoadDLCs |> ForSelf)
    | LoadDLCs(Finished dlcs) ->
        { state with KnownDLCs = Deferred.Resolved(dlcs) }, Cmd.none
    | CopyToClipBoard x ->
        let copy (str) = task {
            do! Application.Current.Clipboard.SetTextAsync str
            return NoOp |> ForSelf
        }
        state, Cmd.OfTask.result (copy x)
    | Execute dlc ->
        let execDLC g (d: KnownDLC) = task {
            Debug.Assert(d.State.OracleInfo |> isNull |> not)
            Debug.Assert(d.State.BuilderState |> isNull |> not && d.State.FundKeyPath |> isNull |> not)
            let repo = ConfigUtils.repository g
            let oracleKey = d.Attestations.Values.First()
            let builder = DLCTransactionBuilder(d.State.BuilderState.ToString(), g.Network);
            let! key = repo.GetKey(d.State.FundKeyPath);
            let execution = builder.Execute(key, oracleKey)
            return execution.CET
        }
        let onSuccess = SetExecutionResult >> ForParent
        let onError (e: exn) = e.Message |> InvalidInput |> ForSelf
        state, Cmd.OfTask.either (execDLC globalConfig) dlc onSuccess onError
    | InvalidInput s ->
        { state with ErrorMsg = Some s }, Cmd.none
    | NoOp ->
        state, Cmd.none
        
let view globalConfig (state: State) dispatch =
    DockPanel.create [
        DockPanel.children [
            match state.KnownDLCs with
            | HasNotStartedYet ->
                ()
            | InProgress ->
                Components.spinner
            | Resolved(Ok dlcs) ->
                StackPanel.create [
                    StackPanel.children [
                        TextBlock.create [
                            TextBlock.text "List of Known DLCs"
                            TextBlock.fontSize 20.
                        ]
                        let column1Width = 180.
                        let column2Width = 140.
                        let column3Width = 300.
                        Border.create [
                            Border.borderThickness 0.5
                            Border.borderBrush (Brushes.WhiteSmoke)
                            Border.child (
                                DockPanel.create [
                                    DockPanel.children [
                                        TextBlock.create [
                                            TextBlock.fontSize 14.
                                            TextBlock.width column1Width
                                            TextBlock.text ("Event Name")
                                            TextBlock.margin 4.
                                        ]
                                        TextBlock.create [
                                            TextBlock.fontSize 14.
                                            TextBlock.width column2Width
                                            TextBlock.text "LocalName"
                                            TextBlock.margin 4.
                                        ]
                                        TextBlock.create [
                                            TextBlock.fontSize 14.
                                            TextBlock.width column3Width
                                            TextBlock.text "Outcomes"
                                            TextBlock.margin 4.
                                        ]
                                        TextBlock.create [
                                            TextBlock.fontSize 14.
                                            TextBlock.dock Dock.Right
                                            TextBlock.text ("Next step")
                                            TextBlock.margin 4.
                                        ]
                                    ]
                                ]
                            )
                        ]
                        ListBox.create [
                            ListBox.dataItems dlcs
                            ListBox.itemTemplate
                                (DataTemplateView<KnownDLC>.create (fun d ->
                                    DockPanel.create [
                                        DockPanel.lastChildFill false
                                        DockPanel.contextMenu (ContextMenu.create [
                                            ContextMenu.viewItems [
                                                MenuItem.create [
                                                    MenuItem.header "Copy"
                                                    MenuItem.viewItems [
                                                        MenuItem.create [
                                                            MenuItem.header "LocalName"
                                                            MenuItem.onClick(fun _ ->
                                                                CopyToClipBoard (d.LocalName) |> ForSelf |> dispatch
                                                                )
                                                        ]
                                                        MenuItem.create [
                                                            MenuItem.header "EventName"
                                                            MenuItem.onClick(fun _ ->
                                                                CopyToClipBoard (d.EventName.ToString()) |> ForSelf |> dispatch
                                                                )
                                                        ]
                                                        MenuItem.create [
                                                            MenuItem.header "Outcomes"
                                                            MenuItem.onClick(fun _ ->
                                                                CopyToClipBoard (d.Outcomes |> Seq.reduce(fun x acc -> x + ", " + acc)) |> ForSelf |> dispatch
                                                                )
                                                        ]
                                                        MenuItem.create [
                                                            MenuItem.header "sha256 Outcome id"
                                                            MenuItem.onClick(fun _ ->
                                                                CopyToClipBoard (d.State.Id.ToString()) |> ForSelf |> dispatch
                                                                )
                                                        ]
                                                        
                                                        let n = globalConfig.Network
                                                        let onRefundTx _ =
                                                            let builder = DLCTransactionBuilder(d.State.BuilderState.ToString(), n)
                                                            let tx = builder.BuildRefund()
                                                            tx.ToHex() |> CopyToClipBoard |> ForSelf |> dispatch
                                                        match d.State.GetNextStep(n), d.IsInitiator with
                                                        | Repository.DLCState.DLCNextStep.Setup, _ ->
                                                            ()
                                                        | Repository.DLCState.DLCNextStep.CheckSigs, true ->
                                                            // async
                                                            MenuItem.create [
                                                                MenuItem.header "Offer (Base64)"
                                                                MenuItem.onClick(fun _ -> CopyToClipBoard(Encoders.Base64.EncodeData(UTF8Encoding.UTF8.GetBytes(d.State.Offer.ToString()))) |> ForSelf |> dispatch)
                                                            ]
                                                            MenuItem.create [
                                                                MenuItem.header "Offer (Json)"
                                                                MenuItem.onClick(fun _ -> CopyToClipBoard(d.State.Offer.ToString()) |> ForSelf |> dispatch)
                                                            ]
                                                        | Repository.DLCState.DLCNextStep.CheckSigs, false ->
                                                            MenuItem.create [
                                                                MenuItem.header "Accept (Base64)"
                                                                MenuItem.onClick(fun _ -> CopyToClipBoard(Encoders.Base64.EncodeData(UTF8Encoding.UTF8.GetBytes(d.State.Accept.ToString()))) |> ForSelf |> dispatch)
                                                            ]
                                                            MenuItem.create [
                                                                MenuItem.header "Accept (Json)"
                                                                MenuItem.onClick(fun _ -> CopyToClipBoard(d.State.Accept.ToString()) |> ForSelf |> dispatch)
                                                            ]
                                                        | Repository.DLCState.DLCNextStep.Fund, true ->
                                                            MenuItem.create [
                                                                MenuItem.header "Funding PSBT (Base64)"
                                                                MenuItem.onClick(fun _ ->
                                                                    let psbt = (DLCTransactionBuilder(d.State.BuilderState.ToString(), n)).GetFundingPSBT()
                                                                    psbt.ToBase64() |> CopyToClipBoard |> ForSelf |> dispatch)
                                                            ]
                                                            MenuItem.create [
                                                                MenuItem.header "Funding PSBT (Json)"
                                                                MenuItem.onClick(fun _ ->
                                                                    let psbt = (DLCTransactionBuilder(d.State.BuilderState.ToString(), n)).GetFundingPSBT()
                                                                    psbt.ToString() |> CopyToClipBoard |> ForSelf |> dispatch)
                                                            ]
                                                        | Repository.DLCState.DLCNextStep.Fund, false ->
                                                            MenuItem.create [
                                                                MenuItem.header "Refund TX (Hex)"
                                                                MenuItem.onClick(onRefundTx)
                                                            ]
                                                        | Repository.DLCState.DLCNextStep.Done, true ->
                                                            MenuItem.create [
                                                                MenuItem.header "Abort PSBT"
                                                                MenuItem.onClick(fun _ ->
                                                                    d.State.Abort.ToBase64() |> CopyToClipBoard |> ForSelf |> dispatch)
                                                            ]
                                                            MenuItem.create [
                                                                MenuItem.header "Refund TX (Hex)"
                                                                MenuItem.onClick(onRefundTx)
                                                            ]
                                                        | Repository.DLCState.DLCNextStep.Done, false ->
                                                            MenuItem.create [
                                                                MenuItem.header "Funding PSBT (Json)"
                                                                MenuItem.onClick(fun _ ->
                                                                    let psbt = (DLCTransactionBuilder(d.State.BuilderState.ToString(), globalConfig.Network)).GetFundingPSBT()
                                                                    psbt.ToString() |> CopyToClipBoard |> ForSelf |> dispatch)
                                                            ]
                                                            MenuItem.create [
                                                                MenuItem.header "Refund TX (Hex)"
                                                                MenuItem.onClick(onRefundTx)
                                                            ]
                                                        | _ ->
                                                            Debug.Assert(true, "Unreachable!")
                                                            ()
                                                    ]
                                                ]
                                                if (d.Attestations.Count = 0) then ()
                                                else if (d.Attestations.Count = 1) then
                                                    MenuItem.create [
                                                        MenuItem.header "Execute"
                                                        MenuItem.onClick(fun _ -> Execute d |> ForSelf |> dispatch)
                                                    ] 
                                                else
                                                    MenuItem.create [
                                                        MenuItem.header "Extract PrivateKey for the Oracle (TODO)"
                                                    ]
                                                if (d.State.GetNextStep(globalConfig.Network) = Repository.DLCState.DLCNextStep.Done) then () else
                                                MenuItem.create [
                                                    MenuItem.header "Goto Next Step"
                                                    MenuItem.onClick(fun _ ->
                                                        { GotoInfo.IsInitiator = d.IsInitiator
                                                          KnownDLC = d
                                                          }
                                                        |> GoToNextStep
                                                        |> ForParent
                                                        |> dispatch
                                                        )
                                                ]
                                            ]
                                        ])
                                        DockPanel.children [
                                            TextBlock.create [
                                                TextBlock.width column1Width
                                                TextBlock.text (d.EventName.ToString())
                                                TextBlock.margin 4.
                                            ]
                                            TextBlock.create [
                                                TextBlock.width column2Width
                                                TextBlock.text d.LocalName
                                                TextBlock.margin 4.
                                            ]
                                            TextBlock.create [
                                                TextBlock.width column3Width
                                                TextBlock.text (d.Outcomes |> Seq.reduce(fun x acc -> x + ", " + acc))
                                                TextBlock.margin 4.
                                            ]
                                            TextBlock.create [
                                                TextBlock.dock Dock.Right
                                                TextBlock.text (d.State.GetNextStep(globalConfig.Network).ToString())
                                                TextBlock.margin 4.
                                            ]
                                        ]
                                    ]
                                ))
                        ]
                    ]
                ]
            | Resolved(Error msg) ->
                TextBlock.create [
                    TextBlock.classes ["error"]
                    TextBlock.text msg
                ]
        ]
    ]
