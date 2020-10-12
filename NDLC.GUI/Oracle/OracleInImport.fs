module NDLC.GUI.Oracle.OracleInImportModule

open System
open System.Diagnostics
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Elmish
open NDLC.Infrastructure
open NDLC.GUI
open NDLC.GUI.Oracle.DomainModel

type InternalMsg =
    | UpdateName of string
    | UpdatePubKey of string
    | Reset

type OutMsg =
    | NewOracle of OracleInfo
type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg

type State = private {
    _OracleName: string
    _PubKey: string
}
    with
    member this.OracleInfo = failwith "TODO"
    member this.ValidateOracleName() =
        if (this._OracleName |> String.IsNullOrWhiteSpace) then Some ("You must specify the oracle name") else
        None
    member this.ValidatePubKey() =
        if (this._PubKey |> String.IsNullOrWhiteSpace) then Some ("You must specify the PubKey") else
        match OracleId.TryParse(this._PubKey.ToLowerInvariant().Trim()) with
        | true, _ ->
            None
        | _ -> Some("Failed to parse PubKey!")
    member this.IsValid =
        match this.ValidateOracleName(), this.ValidatePubKey() with
        | None, None -> true
        | _ -> false
        
    member this.ToOracle =
        Debug.Assert(this.IsValid)
        {
            Name = this._OracleName
            OracleId = this._PubKey |> OracleId.TryParse |> snd
            KeyPath = None
        }
type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
    OnNewOracle: OracleInfo -> 'Msg
}

type Translator<'Msg> = Msg -> 'Msg

let init =
    { _OracleName = ""; _PubKey = "" }

let translator ({ OnInternalMsg = onInternalMsg; OnNewOracle = onNewOracle }: TranslationDictionary<'Msg>): Translator<'Msg> =
    function
    | ForSelf i -> onInternalMsg i
    | ForParent(NewOracle o) -> onNewOracle o
    
let update (msg: InternalMsg) (state: State) =
    match msg with
    | UpdateName s ->
        { state with _OracleName = s }, Cmd.none
    | UpdatePubKey s ->
        { state with _PubKey = s }, Cmd.none
    | Reset -> init, Cmd.none
let view (state: State) dispatch =
    StackPanel.create [
        StackPanel.orientation Orientation.Vertical
        StackPanel.children [
            TextBox.create [
                TextBox.name "oracle name"
                TextBox.classes ["userinput"]
                TextBox.watermark "oracle name to identify the oracle"
                TextBox.errors (state.ValidateOracleName() |> Option.toList |> Seq.cast<obj>)
                yield! TextBox.onTextInput(UpdateName >> ForSelf >> dispatch)
                TextBox.text (state._OracleName)
            ]
            TextBox.create [
                TextBox.name "oracleId"
                TextBox.classes ["userinput"]
                TextBox.watermark "hex-encoded pubkey of the oracle"
                TextBox.errors (state.ValidatePubKey() |> Option.toList |> Seq.cast<obj>)
                yield! TextBox.onTextInput(UpdatePubKey >> ForSelf >> dispatch)
                TextBox.text (state._PubKey)
            ]
            Button.create [
                Button.horizontalAlignment HorizontalAlignment.Right
                Button.classes["round"]
                Button.content "Save"
                Button.isEnabled (state.IsValid)
                Button.onClick((fun _ -> dispatch (ForParent (NewOracle state.ToOracle)); dispatch(ForSelf(Reset))),
                               SubPatchOptions.OnChangeOf(state))
            ]
        ]
    ]
