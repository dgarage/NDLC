namespace NDLC.GUI

open Avalonia.Controls
open Avalonia.FuncUI.Builder
open Avalonia.FuncUI.DSL
open Elmish

module Router =
    type Page =
        | About
        | Oracle
        | DLC
        
    type State =
        { CurrentPage: Page
          OracleState: OracleModule.State
          DLCState: DLCModule.State
          }
        
    type Msg =
        | NavigateTo of Page
        | OracleMsg of OracleModule.Msg
        | DLCMsg of DLCModule.Msg
        
    let init =
        let o, oCmd = OracleModule.init
        let d, dCmd = DLCModule.init
        { CurrentPage = Page.About
          OracleState = o
          DLCState = d
        }, Cmd.batch [(oCmd |> Cmd.map(OracleMsg)); (dCmd |> Cmd.map(DLCMsg))]
        
    let update globalConfig (msg: Msg) (state: State) =
        match msg with
        | NavigateTo page ->
            { state with CurrentPage = page }, Cmd.none
        | OracleMsg m ->
            let newState, cmd = OracleModule.update globalConfig m (state.OracleState)
            { state with OracleState = newState }, cmd |> Cmd.map(OracleMsg)
        | DLCMsg m ->
            let newState, cmd = DLCModule.update globalConfig m (state.DLCState)
            { state with DLCState = newState }, (cmd |> Cmd.map(DLCMsg))
            
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
                    | DLC ->
                        yield DLCModule.view state.DLCState (DLCMsg >> dispatch)
                ]
            ]
