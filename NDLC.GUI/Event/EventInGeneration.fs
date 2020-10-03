module NDLC.GUI.Event.EventInGenerationModule

open Avalonia.FuncUI.DSL

open Avalonia.Controls
open Avalonia.Layout

open NDLC.GUI

type EventGenerationArg = {
    Name: string
    Outcomes: string []
}
    with
    static member Empty = { Name = ""; Outcomes = [||] }
    
type EventGenerationMsg =
    | SetName of string
    | SetOutcomes of string[]
type InternalMsg =
    | UpdateGenerate of EventGenerationMsg
    | InvalidInput of string
    
type OutMsg =
    | Generate of EventGenerationArg
    
type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg
    
type State = {
    EventInGeneration: EventGenerationArg
    ErrorMsg: string option
}

type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
    OnGenerate: EventGenerationArg -> 'Msg
}

type Translator<'Msg> = Msg -> 'Msg

let translator ({ OnInternalMsg = onInternalMsg; OnGenerate = onGenerate; }: TranslationDictionary<'Msg>): Translator<'Msg> =
    function
    | ForSelf i -> onInternalMsg i
    | ForParent (Generate x) -> onGenerate x

let init =
    { EventInGeneration = EventGenerationArg.Empty; ErrorMsg = None }

let update (msg: InternalMsg) (state: State) =
    match msg with
    | UpdateGenerate a ->
        match a with
        | SetName n ->
            let e = { state.EventInGeneration with Name = n }
            { state with EventInGeneration = e }
        | SetOutcomes o ->
            let e = { state.EventInGeneration with Outcomes = o }
            { state with EventInGeneration = e }
    | InvalidInput i ->
        { state with ErrorMsg = Some i }

let eventInGenerationView (state: EventGenerationArg) dispatch =
    StackPanel.create [
        StackPanel.orientation Orientation.Vertical
        StackPanel.name "Please Enter Information about new Event"
        StackPanel.children [
            TextBox.create [
                TextBox.watermark "Enter event name here"
                TextBox.name "EventNameBox"
                TextBox.text state.Name
                TextBox.isReadOnly false
                yield! TextBox.onTextInputFinished(fun txt -> SetName txt |> UpdateGenerate |> ForSelf |> dispatch)
            ]
            TextBox.create [
                TextBox.watermark "Enter comma separated list of outcomes (e.g. \"Sunny,Cloudy,Else\")"
                TextBox.name "OutcomesBox"
                TextBox.isReadOnly false
                TextBox.text (state.Outcomes |> String.concat ",")
                yield! TextBox.onTextInputFinished(fun txt -> txt.Split"," |> SetOutcomes |> UpdateGenerate |> ForSelf |> dispatch)
            ]
            Button.create [
                Button.horizontalAlignment HorizontalAlignment.Right
                Button.content "Save"
                Button.onClick((fun _ -> state |> Generate |> ForParent |> dispatch), SubPatchOptions.OnChangeOf(state))
            ]
        ]
    ]
    
let view (state: State) dispatch =
    StackPanel.create [
        StackPanel.children [
            eventInGenerationView state.EventInGeneration dispatch
                
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

