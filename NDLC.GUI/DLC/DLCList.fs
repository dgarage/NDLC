[<RequireQualifiedAccess>]
module NDLC.GUI.DLCListModule

open Avalonia.Controls
open Avalonia.FuncUI.Components
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open System.Linq
open FSharp.Control.Tasks
open Elmish
open NBitcoin
open NDLC.GUI
open NDLC.GUI.Utils
open NDLC.Infrastructure


type KnownDLCs = private {
    LocalName: string
    State: (Repository.DLCState)
}

type State = private {
    KnownDLCs: Deferred<Result<KnownDLCs seq, string>>
}

type InternalMsg =
    private
    | LoadDLCs of AsyncOperationStatus<Result<KnownDLCs seq, string>>
    
type GotoInfo = {
    NextStep: Repository.DLCState.DLCNextStep
    LocalName: string
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
            for dlcName in dlcs.OrderBy(fun o -> o.Key.ToString()) do
                let! dState = repo.GetDLC(uint256(dlcName.Value))
                result.Add({ LocalName = dlcName.Key; State = dState })
            return result :> seq<_>
        }
        { state with KnownDLCs = InProgress },
        Cmd.OfTask.either (load) globalConfig
            (Ok >> Finished >> LoadDLCs)
            (fun e -> e.Message |> Error |> Finished |> LoadDLCs)
    | LoadDLCs(Finished dlcs) ->
        { state with KnownDLCs = Deferred.Resolved(dlcs) }, Cmd.none
        
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
                        TextBox.create [
                            TextBox.text "List of Known DLCs"
                            TextBox.fontSize 20.
                        ]
                        ListBox.create [
                            ListBox.dataItems dlcs
                            ListBox.itemTemplate
                                (DataTemplateView<KnownDLCs>.create (fun d -> StackPanel.create [
                                    StackPanel.orientation Orientation.Horizontal
                                    StackPanel.children [
                                        TextBlock.create [
                                            TextBlock.text d.LocalName
                                            TextBlock.margin 4.
                                        ]
                                        TextBlock.create [
                                            TextBlock.text (d.State.Id.ToString())
                                            TextBlock.margin 4.
                                        ]
                                        TextBlock.create [
                                            TextBlock.text (d.State.GetNextStep(globalConfig.Network).ToString())
                                            TextBlock.margin 4.
                                        ]
                                    ]
                                ]))
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
