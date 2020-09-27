namespace NDLC.GUI

open Avalonia.Controls
open Avalonia.FuncUI.Builder
open Avalonia.FuncUI.DSL
open Elmish

module Router =
    type Page =
        | About
        | Oracle
        
    type State =
        { CurrentPage: Page
          OracleState: OracleModule.State
          }
        
    type Msg =
        | NavigateTo of Page
        | OracleMsg of OracleModule.Msg
        
    let init =
        let o, cmd = OracleModule.init
        { CurrentPage = Page.About
          OracleState = o
        }, cmd |> Cmd.map(OracleMsg)
        
    let update globalConfig (msg: Msg) (state: State) =
        match msg with
        | NavigateTo page ->
            { state with CurrentPage = page }, Cmd.none
        | OracleMsg m ->
            let newState, cmd = OracleModule.update globalConfig m (state.OracleState)
            { state with OracleState = newState }, cmd |> Cmd.map(OracleMsg)
            
    let viewMenu _ dispatch =
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
                ]
            ]
