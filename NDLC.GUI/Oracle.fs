namespace NDLC.GUI

open NBitcoin
open FSharp.Control.Tasks

open System.Threading.Tasks
open Avalonia.Controls
open Avalonia.FuncUI.Builder
open Avalonia.FuncUI.Components
open Avalonia.FuncUI.Components.Hosts
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Elmish
open Avalonia.Layout
open Avalonia.Media
open Elmish
open NDLC.GUI.Utils
open NDLC.Infrastructure
            
module OracleModule =
    open NDLC.GUI.Utils
    type OracleInfo = {
        Name: string
        PubKeyHex: string
    }
    type State =
        {
            KnownOracles: Deferred<Result<OracleInfo seq, string>>
            Selected: OracleInfo option
        }
    type Msg =
        | LoadOracles of AsyncOperationStatus<Result<OracleInfo seq, string>>
        | Remove of OracleInfo
        | Select of OracleInfo option
        
    let loadOracleInfos(repo: NameRepository) =
        task {
            let! a =
                repo.AsOracleNameRepository().GetIds()
            return
                a
                |> Seq.sortBy(fun kv -> kv.Key)
                |> Seq.map(fun kv -> { OracleInfo.Name = kv.Key; PubKeyHex = kv.Value.ToString() })
                |> Ok
                |> Finished
                |> LoadOracles
        }
        
    let removeOracle (repo: NameRepository) oracle = task {
        let! isOk = repo.RemoveMapping(Scopes.Oracles, oracle.Name)
        return ()
    }
    let init (config: NDLC.GUI.Utils.GlobalConfig) =
        { KnownOracles = Deferred.HasNotStartedYet; Selected = None }, Cmd.ofMsg(LoadOracles Started)
        
    let update (globalConfig) (msg: Msg) (state: State) =
        match msg with
        | LoadOracles Started ->
            let nameRepo =  (ConfigUtils.nameRepo globalConfig)
            { state with KnownOracles = InProgress }, Cmd.OfTask.result (loadOracleInfos nameRepo)
        | LoadOracles (Finished (Ok oracles)) ->
            { state with KnownOracles = Resolved(Ok oracles) }, Cmd.none
        | LoadOracles (Finished (Error e)) ->
            {state with KnownOracles = Resolved(Error e)} , Cmd.none
        | Remove oracle ->
            let nameRepo = ConfigUtils.nameRepo globalConfig
            (removeOracle nameRepo oracle).GetAwaiter().GetResult()
            let newOracles = state.KnownOracles |> Deferred.map(Result.map(Seq.where(fun o -> o <> oracle)))
            { state with KnownOracles = newOracles }, Cmd.none
        | Select o ->
            {state with Selected = o}, Cmd.none
        
        
    let oracleListView (oracles: OracleInfo seq) dispatch =
        ListBox.create [
            ListBox.dock Dock.Left
            ListBox.onSelectedItemChanged(fun obj ->
                match obj with
                | :? OracleInfo as o -> o |> Some
                | _ -> None
                |> Select |> dispatch
            )
            ListBox.dataItems (oracles)
            ListBox.itemTemplate
                (DataTemplateView<OracleInfo>.create (fun d ->
                DockPanel.create [
                    DockPanel.lastChildFill false
                    DockPanel.children [
                        TextBlock.create [
                            TextBlock.text d.Name
                            TextBlock.margin 5.
                        ]
                        TextBlock.create [
                            TextBlock.text d.PubKeyHex
                            TextBlock.margin 5.
                        ]
                        Button.create [
                            Button.dock Dock.Right
                            Button.content "remove"
                            Button.onClick ((fun _ -> d |> Msg.Remove |> dispatch), SubPatchOptions.OnChangeOf d.PubKeyHex)
                        ]
                    ]
                ]
                ))
        ]
        
        
    let spinner =
        Grid.create [
            Grid.children [
                StackPanel.create [
                    StackPanel.horizontalAlignment HorizontalAlignment.Center
                    StackPanel.verticalAlignment VerticalAlignment.Center
                    StackPanel.children [
                        TextBox.create [
                            TextBox.text "Now loading..."
                        ]
                    ]
                ]
            ]
        ]
    let renderError (errorMsg: string) =
        Grid.create [
            Grid.children [
                    StackPanel.create [
                    StackPanel.dock Dock.Top
                    StackPanel.verticalAlignment VerticalAlignment.Center
                    StackPanel.children [
                        TextBlock.create [
                            TextBlock.dock Dock.Top
                            TextBlock.classes ["h1"]
                            TextBlock.fontSize 24.
                            TextBlock.fontWeight FontWeight.Thin
                            TextBlock.text errorMsg
                        ]
                    ]
                ]
            ]
        ]
    let viewOracle dispatch o =
        match o with
        | HasNotStartedYet -> Grid.create []
        | InProgress -> spinner
        | Resolved(Ok items) ->
            Grid.create [
                Grid.rowDefinitions "Auto,*"
                Grid.children [
                    TextBlock.create [
                        TextBlock.text (sprintf "List of known oracles: %i." (items |> Seq.length))
                    ]
                    oracleListView items dispatch
                ]
            ]
        | Resolved(Error e) ->
            renderError e
    
    let oracleDetailsView (state: OracleInfo option) =
        DockPanel.create [
            DockPanel.dock Dock.Right
            DockPanel.isVisible state.IsSome
            DockPanel.width 250.
            DockPanel.children [
                match state with
                | None -> ()
                | Some o ->
                    StackPanel.create [
                        StackPanel.dock Dock.Top
                        StackPanel.orientation Orientation.Vertical
                        StackPanel.margin 10.
                        StackPanel.children [
                            TextBlock.create [
                                TextBlock.text "Here goes oracle details"
                            ]
                        ]
                    ]
            ]
        ]
    let view (state: State) dispatch =
        DockPanel.create [
            DockPanel.children [
                viewOracle dispatch state.KnownOracles
                oracleDetailsView state.Selected
            ]
        ]
        
