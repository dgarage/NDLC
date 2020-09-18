namespace NDLC.GUI

open Avalonia.Controls
open Avalonia.FuncUI.Builder
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.Styling

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
        
    let init =
        { CurrentPage = Page.About
          OracleState = OracleModule.init
          EventState = EventModule.init
          DLCState = DLCModule.init }
        
    type Msg =
        | NavigateTo of Page
        | OracleMsg of OracleModule.Msg
        | EventMsg of EventModule.Msg
        | DLCMsg of DLCModule.Msg
        
    let update (msg: Msg) (state: State) =
        match msg with
        | NavigateTo page ->
            { state with CurrentPage = page }
        | _ -> failwith "Unreachable"
            
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
