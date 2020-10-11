[<RequireQualifiedAccess>]
module NDLC.GUI.DLCListModule

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
open NBitcoin.DataEncoders
open NDLC.GUI
open NDLC.GUI.Utils
open NDLC.Infrastructure
open NDLC.Messages


type KnownDLCs = private {
    EventName: EventFullName
    LocalName: string
    State: (Repository.DLCState)
    Outcomes: string []
    IsInitiator: bool
}

type State = private {
    KnownDLCs: Deferred<Result<KnownDLCs seq, string>>
}

type InternalMsg =
    | LoadDLCs of AsyncOperationStatus<Result<KnownDLCs seq, string>>
    | CopyToClipBoard of string
    | NoOp
    
type GotoInfo = {
    LocalName: string
    DLCState: Repository.DLCState
    IsInitiator: bool
}
type OutMsg =
    | GoToNextStep of GotoInfo
    
type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg
    
type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
    OnGoToNextStep: GotoInfo -> 'Msg
}

type Translator<'Msg> = Msg -> 'Msg

let translator ({ OnInternalMsg = onInternalMsg; OnGoToNextStep = onInputFinished }: TranslationDictionary<'Msg>): Translator<'Msg> =
    function
    | ForSelf i -> onInternalMsg i
    | ForParent(GoToNextStep info) -> onInputFinished info
    
let init =
    { KnownDLCs = HasNotStartedYet }, Cmd.ofMsg(LoadDLCs Started)
    
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
                let info =
                    let builder = DLCTransactionBuilder(dState.BuilderState.ToString(), globalConfig.Network)
                    { LocalName = dlcName; State = dState; IsInitiator = builder.State.IsInitiator; EventName = eventFullName; Outcomes = e.Outcomes }
                result.Add(info)
            return result.OrderBy(fun x -> (x.EventName.ToString(), x.LocalName)) :> seq<_>
        }
        { state with KnownDLCs = InProgress },
        Cmd.OfTask.either (load) globalConfig
            (Ok >> Finished >> LoadDLCs)
            (fun e -> e.ToString() |> Error |> Finished |> LoadDLCs)
    | LoadDLCs(Finished dlcs) ->
        { state with KnownDLCs = Deferred.Resolved(dlcs) }, Cmd.none
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
                                (DataTemplateView<KnownDLCs>.create (fun d ->
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
                                                MenuItem.create [
                                                    MenuItem.header "Goto Next Step"
                                                    MenuItem.onClick(fun _ ->
                                                        { GotoInfo.IsInitiator = d.IsInitiator
                                                          LocalName = d.LocalName
                                                          DLCState = d.State }
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
