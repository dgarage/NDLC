module NDLC.GUI.Event.EventInImportModule

open System

open Avalonia.Controls
open Avalonia.Layout

open Avalonia.FuncUI.DSL
open NDLC.Secp256k1

open NDLC.GUI

type EventImportArg = {
    _EventName: string
    _Nonce: string
    _Outcomes: string
    _OracleName: string
}
    with
    member this.EventName = this._EventName
    member this.Nonce =
        match SchnorrNonce.TryParse this._Nonce with
        | true, nonce -> nonce |> Some
        | _ -> None
    member this.Outcomes = this._Outcomes.Split","
    member this.ValidateNonce() =
        this.Nonce |> function None -> Some("Invalid Nonce") | Some _ -> None
    member this.ValidateOutcomes() =
        if this._Outcomes |> String.IsNullOrEmpty then Some("You must input list of outcomes splitted by ','") else
        if this.Outcomes.Length <= 1 then Some("You must specify at least two outcomes") else
        if (this.Outcomes |> Seq.distinct |> Seq.length <> this.Outcomes.Length) then Some("All outcomes must be unique") else
        None
    member this.ValidateEventName() =
        (if this.EventName |> String.IsNullOrWhiteSpace then Some ("You must set some event name") else None)
    member this.IsValid() =
        match this.ValidateOutcomes(), this.ValidateNonce(), this.ValidateEventName() with
        | None, None, None -> true
        | _ -> false
    static member Empty = {
        _EventName = ""
        _Nonce = ""
        _Outcomes = ""
        _OracleName = ""
    }
    
type InternalMsg =
    | UpdateEventName of string
    | UpdateNonce of string
    | UpdateOutcomes of string
    | InvalidInput of string
    | Reset
    
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
    | UpdateEventName a ->
        { state with EventInImport = { state.EventInImport with _EventName = a } }
    | UpdateNonce a ->
        { state with EventInImport = { state.EventInImport with _Nonce = a } }
    | UpdateOutcomes a ->
        { state with EventInImport = { state.EventInImport with _Outcomes = a }}
    | InvalidInput i ->
        { state with ErrorMsg = Some i }
    | Reset -> init

let eventInImportView (state: EventImportArg) dispatch =
    StackPanel.create [
        StackPanel.orientation Orientation.Vertical
        StackPanel.name "Please Enter Information about new Event"
        let name1 = "EventName"
        let name2 = "Nonce"
        let name3 = "Outcomes"
            
        StackPanel.children [
            TextBox.create [
                TextBox.name name1
                TextBox.watermark "Enter event name here"
                TextBox.classes ["userinput"]
                yield! TextBox.onTextInput(UpdateEventName >> ForSelf >> dispatch)
                TextBox.errors(
                     state.ValidateEventName()
                     |> Option.toList |> Seq.cast<obj>
                     )
                TextBox.text state._EventName
            ]
            TextBox.create [
                TextBox.name name2
                TextBox.classes ["userinput"]
                TextBox.errors (state.ValidateNonce() |> Option.toList |> Seq.cast<obj>)
                yield! TextBox.onTextInput(UpdateNonce >> ForSelf >> dispatch)
                TextBox.watermark "Paste Nonce here"
                TextBox.text state._Nonce
            ]
            TextBox.create [
                TextBox.name name3
                TextBox.classes ["userinput"]
                TextBox.errors (state.ValidateOutcomes() |> Option.toList |> Seq.cast<obj>)
                yield! TextBox.onTextInput(UpdateOutcomes >> ForSelf >> dispatch)
                TextBox.watermark "Enter comma separated list of outcomes (e.g. \"Sunny,Cloudy,Else\")"
                TextBox.text state._Outcomes
            ]
            Button.create [
                Button.horizontalAlignment HorizontalAlignment.Right
                Button.classes["round"]
                Button.content "Save"
                Button.isEnabled (state.IsValid())
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
    

