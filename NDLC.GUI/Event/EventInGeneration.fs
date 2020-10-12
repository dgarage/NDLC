module NDLC.GUI.Event.EventInGenerationModule

open System
open Avalonia.FuncUI.DSL

open Avalonia.Controls
open Avalonia.Layout

open NDLC.GUI

type EventGenerationArg = private {
    _Name: string
    _Outcomes: string
}
    with
    member this.Name = this._Name
    member this.Outcomes = this._Outcomes.Split "," |> Array.where(String.IsNullOrWhiteSpace >> not)
    static member Empty = { _Name = ""; _Outcomes = "" }
    member this.ValidateName() =
        if this._Name |> String.IsNullOrEmpty then
            Some("You must input event name")
        else
            None
    member this.ValidateOutcomes() =
        if this._Outcomes |> String.IsNullOrEmpty then Some("You must input list of outcomes splitted by ','") else
        if this.Outcomes.Length <= 1 then Some("You must specify at least two outcomes") else
        if (this.Outcomes |> Seq.distinct |> Seq.length <> this.Outcomes.Length) then Some("All outcomes must be unique") else
        None
        
    member this.IsValid() =
        match this.ValidateOutcomes(), this.ValidateName() with
        | None, None -> true
        | _ -> false
        
    
type EventGenerationMsg =
    | SetName of string
    | SetOutcomes of string
type InternalMsg =
    | Reset
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
    | Reset ->
        init
    | UpdateGenerate a ->
        match a with
        | SetName n ->
            let e = { state.EventInGeneration with _Name = n }
            { state with EventInGeneration = e }
        | SetOutcomes o ->
            let e = { state.EventInGeneration with _Outcomes = o }
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
                TextBox.classes ["userinput"]
                TextBox.name "EventNameBox"
                TextBox.text state.Name
                TextBox.errors (state.ValidateName() |> Option.toList |> Seq.cast<obj>)
                TextBox.isReadOnly false
                yield! TextBox.onTextInput(fun txt -> SetName txt |> UpdateGenerate |> ForSelf |> dispatch)
            ]
            TextBox.create [
                TextBox.watermark "Enter comma separated list of outcomes (e.g. \"Sunny,Cloudy,Else\")"
                TextBox.name "OutcomesBox"
                TextBox.classes ["userinput"]
                TextBox.isReadOnly false
                TextBox.errors (state.ValidateOutcomes() |> Option.toList |> Seq.cast<obj>)
                TextBox.text (state._Outcomes)
                yield! TextBox.onTextInput(SetOutcomes >> UpdateGenerate >> ForSelf >> dispatch)
            ]
            Button.create [
                Button.horizontalAlignment HorizontalAlignment.Right
                Button.classes["round"]
                Button.isEnabled (state.IsValid())
                Button.content "Save"
                Button.onClick((fun _ -> state |> Generate |> ForParent |> dispatch;), SubPatchOptions.OnChangeOf(state))
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

