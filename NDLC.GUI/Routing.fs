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
        | OracleMsg of OracleModule.InternalMsg
        | DLCMsg of DLCModule.Msg
        | Sequence of Msg seq
        
    let oracleMsgTranslator =
        OracleModule.translator {
            OnInternalMsg = OracleMsg
            OnNewOffer = fun offer -> Sequence([NavigateTo(DLC); offer |> DLCOfferModule.NewOffer |> DLCModule.OfferMsg |> DLCMsg])
        }
        
    let init =
        let o, oCmd = OracleModule.init
        let d, dCmd = DLCModule.init
        { CurrentPage = Page.About
          OracleState = o
          DLCState = d
        }, Cmd.batch [(oCmd |> Cmd.map(OracleMsg)); (dCmd |> Cmd.map(DLCMsg))]
        
    let rec update globalConfig (msg: Msg) (state: State) =
        match msg with
        | NavigateTo page ->
            { state with CurrentPage = page }, Cmd.none
        | OracleMsg m ->
            let newState, cmd = OracleModule.update globalConfig m (state.OracleState)
            { state with OracleState = newState }, cmd |> Cmd.map(OracleMsg)
        | DLCMsg m ->
            let newState, cmd = DLCModule.update globalConfig (state.DLCState) m
            { state with DLCState = newState }, (cmd |> Cmd.map(DLCMsg))
        | Sequence msgs ->
           let folder (s, c) msg =
                let s', cmd = update globalConfig msg s
                s', cmd :: c
           let newState, cmdList = msgs |> Seq.fold(folder) (state, [])
           newState, (cmdList |> Cmd.batch)
            
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
    let view globalConfig (state: State) dispatch =
            DockPanel.create [
                DockPanel.children [
                    yield viewMenu state dispatch
                    match state.CurrentPage with
                    | About ->
                        yield (ViewBuilder.Create<About.Host>([]))
                    | Oracle ->
                        yield OracleModule.view state.OracleState (oracleMsgTranslator >> dispatch)
                    | DLC ->
                        yield DLCModule.view globalConfig state.DLCState (DLCMsg >> dispatch)
                ]
            ]
