module NDLC.GUI.Event.EventInImportModule

open Avalonia.Controls
open Avalonia.Layout

open Avalonia.FuncUI.DSL
open NDLC.Secp256k1

open NDLC.GUI

type EventImportArg = {
    OracleName: string
    EventName: string
    Nonce: SchnorrNonce option
    Outcomes: string []
}
    with
    static member Empty = {
        OracleName = ""
        EventName = ""
        Nonce = None
        Outcomes = [||]
    }
    
type InternalMsg =
    | UpdateImport of EventImportArg
    | InvalidInput of string
    
type OutMsg =
    | NewEvent of EventImportArg

type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg
    
type State = {
    EventInImport: EventImportArg
    ErrorMsg: string option
}

type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
    OnNewEvent: EventImportArg -> 'Msg
}

type Translator<'Msg> = Msg -> 'Msg

let translator ({ OnInternalMsg = onInternalMsg; OnNewEvent = onNewEvent }: TranslationDictionary<'Msg>): Translator<'Msg> =
    function
    | ForSelf i -> onInternalMsg i
    | ForParent (NewEvent x) -> onNewEvent x
    
let init =
    { EventInImport = EventImportArg.Empty; ErrorMsg = None }
    
let update (msg: InternalMsg) (state: State) =
    match msg with
    | UpdateImport a ->
        { state with EventInImport = a }
    | InvalidInput i ->
        { state with ErrorMsg = Some i }

let eventInImportView (state: EventImportArg) dispatch =
    StackPanel.create [
        StackPanel.orientation Orientation.Vertical
        StackPanel.name "Please Enter Information about new Event"
        let name1 = "EventName"
        let name2 = "Nonce"
        let name3 = "Outcomes"
        let handler (b: TextBox) =
            match b.Text with
            | s when b.Name = name1 ->
                { state with EventName = s }
                |> UpdateImport
            | s when b.Name = name2 ->
                match SchnorrNonce.TryParse s with
                | true, nonce ->
                    { state with Nonce = Some(nonce) }
                    |> UpdateImport
                | false, _ ->
                    InvalidInput "Failed to parse Nonce"
            | s when b.Name = name3 ->
                { state with Outcomes = s.Split "," }
                |> UpdateImport
            | _ -> failwith "Unreachable!"
            |> ForSelf |> dispatch
            
        yield! StackPanel.onTextboxInput handler
        StackPanel.children [
            TextBox.create [
                TextBox.name name1
                TextBox.watermark "Enter event name here"
                TextBox.text state.EventName
            ]
            TextBox.create [
                TextBox.name name2
                TextBox.watermark "Paste Nonce here"
            ]
            TextBox.create [
                TextBox.name name3
                TextBox.watermark "Enter comma separated list of outcomes (e.g. \"Sunny,Cloudy,Else\")"
            ]
            Button.create [
                Button.content "Save"
                Button.onClick((fun _ -> dispatch (ForParent (NewEvent state))),
                               SubPatchOptions.OnChangeOf(state))
            ]
        ]
    ]
let view (state: State) dispatch =
    StackPanel.create [
        StackPanel.children [
            eventInImportView state.EventInImport dispatch
                
            match state.ErrorMsg with
            | Some s ->
                StackPanel.create [
                    StackPanel.children [
                        TextBlock.create [
                            TextBlock.classes ["error"]
                            TextBlock.text s
                        ]
                    ]
                ]
            | None  -> ()
        ]
    ]
    

