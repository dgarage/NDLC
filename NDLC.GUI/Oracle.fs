namespace NDLC.GUI

open Avalonia.Controls
open Avalonia.Controls
open Avalonia.FuncUI.Builder
open Avalonia.FuncUI.Components.Hosts
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Elmish
open Avalonia.Layout
open Elmish
        
module OracleListModule =
    type OracleInfo = { Name: string; PubKeyHex: string }
    type State = {
        KnownOracles: OracleInfo list
    }
    let init = { KnownOracles = [] }
    
    type Msg = Null
    let update (msg: Msg) (state: State) =
        state
    let view (state: State) dispatch =
        Grid.create [
            Grid.rowDefinitions "Auto,*"
            Grid.children [
                StackPanel.create [
                    StackPanel.orientation Orientation.Vertical
                    StackPanel.children [
                        TextBlock.create [
                            TextBlock.classes ["h1"]
                            TextBlock.text "foo"
                            TextBlock.row 0
                        ]
                        TextBlock.create [
                            TextBlock.classes ["h2"]
                            TextBlock.text "bar"
                            TextBlock.row 1
                        ]
                    ]
                ]
                
                Grid.create [
                    Grid.rowDefinitions "Auto,Auto,Auto,Auto"
                    Grid.children [
                    ]
                ]
            ]
        ]
        
    type Host() as this =
        inherit HostControl()
        do
            Elmish.Program.mkSimple(fun () -> init) update view
            |> Program.withHost this
            |> Program.run
    
module OracleModule =
    type State =
        { KnownOracles: OracleListModule.OracleInfo list }
        
    let init =
        { KnownOracles = [] }
    
    type Msg =
        | Null
        
    let update (msg: Msg) (state: State) =
        state
        
    let view (state: State) dispatch =
        DockPanel.create [
            DockPanel.children [
                TabControl.create [
                    TabControl.tabStripPlacement Dock.Left
                    TabControl.viewItems [
                        TabItem.create [
                            TabItem.header "View Known Oracles list"
                            TabItem.content (ViewBuilder.Create<OracleListModule.Host>([]))
                        ]
                    ]
                ]
            ]
        ]
