namespace NDLC.GUI

open Avalonia.FuncUI.DSL

module EventModule =
    type EventInfo = {
        Oracle: string
        EventName: string
        NonceHex: string
        Outcomes: string list
    }
    with
        member this.FullName =
            sprintf "%s/%s" this.Oracle this.EventName
    type State =
        { Events: EventInfo list }
        
    let init =
        { Events = [] }
    
    type Msg =
        | Null
        
    let update (msg: Msg) (state: State) =
        state
        
    let view (state: State) dispatch =
        Grid.create []
