namespace NDLC.GUI

open Avalonia.Controls
open Avalonia.FuncUI.Builder
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.Styling
open Elmish
open NDLC.GUI.Utils

module Shell =
    type Page =
        | About
        | Oracle
        | Event
        | DLC
        
    type State =
        { CurrentPage: Page
          OracleState: OracleModule.State
          EventState: EventModule.State
          DLCState: DLCModule.State }
        
    type Msg =
        | NavigateTo of Page
        | OracleMsg of OracleModule.Msg
        | EventMsg of EventModule.Msg
        | DLCMsg of DLCModule.Msg
        
    let init =
        let globalConfig = GlobalConfig.Default
        let o, cmd = OracleModule.init globalConfig
        { CurrentPage = Page.About
          OracleState = o
          EventState = EventModule.init
          DLCState = DLCModule.init }, cmd |> Cmd.map(OracleMsg)
        
    let update globalConfig (msg: Msg) (state: State) =
        match msg with
        | NavigateTo page ->
            { state with CurrentPage = page }, Cmd.none
        | OracleMsg m ->
            let newState, cmd = OracleModule.update globalConfig m (state.OracleState)
            { state with OracleState = newState }, cmd |> Cmd.map(OracleMsg)
        | EventMsg m ->
            let newState = EventModule.update m (state.EventState)
            { state with EventState = newState }, Cmd.none
        | DLCMsg m ->
            let newState = DLCModule.update m (state.DLCState)
            { state with DLCState = newState }, Cmd.none
            
    let viewMenu state dispatch =
        Menu.create [
            Menu.viewItems [
                MenuItem.create [
                    MenuItem.onClick (fun _ -> dispatch (NavigateTo About))
                    MenuItem.header "About"
                ]
                MenuItem.create [
                    MenuItem.onClick (fun _ -> dispatch (NavigateTo Oracle))
                    MenuItem.header "Oracle"
                ]
                MenuItem.create [
                    MenuItem.onClick (fun _ -> dispatch (NavigateTo Event))
                    MenuItem.header "Event"
                ]
                MenuItem.create [
                    MenuItem.onClick (fun _ -> dispatch (NavigateTo DLC))
                    MenuItem.header "DLC"
                ]
            ]
            Menu.dock Dock.Top
        ]
        
    let view (state: State) dispatch =
            DockPanel.create [
                DockPanel.children [
                    yield viewMenu state dispatch
                    match state.CurrentPage with
                    | About ->
                        yield (ViewBuilder.Create<About.Host>([]))
                    | Oracle ->
                        yield OracleModule.view state.OracleState (OracleMsg >> dispatch)
                    | Event ->
                        yield EventModule.view state.EventState (EventMsg >> dispatch)
                    | DLC ->
                        yield DLCModule.view state.DLCState (DLCMsg >> dispatch)
                ]
            ]
