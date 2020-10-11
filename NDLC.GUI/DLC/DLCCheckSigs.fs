[<RequireQualifiedAccess>]
module NDLC.GUI.DLCCheckSigsModule

open ResultUtils
open System
open FSharp.Control.Tasks
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open System.Diagnostics
open Elmish
open NBitcoin

open System.Text
open Avalonia
open Avalonia.Media
open NBitcoin.DataEncoders
open NDLC.GUI
open NDLC.GUI.DLC
open NDLC.GUI.DLC
open NDLC.GUI.DLC
open NDLC.GUI.Utils
open NDLC.GUI.Utils
open NDLC.Infrastructure
open NDLC.Messages
open Newtonsoft.Json
open Newtonsoft.Json.Linq

type State = {
    ErrorMsg: string option
    LocalName: string
    AcceptOrSignMsg: string
}

type InternalMsg =
    | InvalidInput of string
    | Reset
    | CopyToClipBoard of string
    | NoOp
    | UpdateAcceptOrSignMsg of string
    | UpdateLocalName of string
    | CommitAcceptOrSignMsg
    
type CheckSigResult = {
    Msg: string
    ExtractedPSBT: PSBT
}
type OutMsg =
    | Finished of CheckSigResult
    
type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg
    
type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
    OnFinished: CheckSigResult -> 'Msg
}

type Translator<'Msg> = Msg -> 'Msg

let translator ({ OnInternalMsg = onInternalMsg; OnFinished = onInputFinished }: TranslationDictionary<'Msg>): Translator<'Msg> =
    function
    | ForSelf i -> onInternalMsg i
    | ForParent(Finished msg) -> onInputFinished msg
    
let init = {
    ErrorMsg = None
    AcceptOrSignMsg = ""
    LocalName = ""
}

[<AutoOpen>]
module private Tasks =
    let validateAcceptOrSignMsg (str: string): Option<_> =
        if (str |> String.IsNullOrWhiteSpace) then Some "Empty Offer msg not allowed" else
        if (str |> String.isBase64 |> not) then Some "Accept/Sign Msg must be base64 encoded" else
        None
        
let update globalConfig msg state =
    match msg with
    | Reset ->
        init, Cmd.none
    | InvalidInput msg ->
        { state with ErrorMsg = Some msg }, Cmd.none
    | CopyToClipBoard s ->
        let copy (str) = task {
            do! Application.Current.Clipboard.SetTextAsync str
            return NoOp |> ForSelf
        }
        state, Cmd.OfTask.result (copy s)
        
    | UpdateAcceptOrSignMsg msg ->
        { state with AcceptOrSignMsg = msg }, Cmd.none
    | UpdateLocalName msg ->
        { state with LocalName = msg }, Cmd.none
    | CommitAcceptOrSignMsg ->
        let onSuccess = fun x -> Finished({ Msg = "Looks good! Please sign the resulted psbt on goto \"Start\""
                                            ExtractedPSBT = x }) |> ForParent
        let onFailure (e: exn) = InvalidInput (e.Message) |> ForSelf
        state, Cmd.OfTask.either (DLC.CheckSig.checksig globalConfig) (state.AcceptOrSignMsg) (onSuccess) (onFailure)
    | NoOp ->
        state, Cmd.none
        
        
let view g (s: State) dispatch =
    StackPanel.create [
        StackPanel.horizontalAlignment HorizontalAlignment.Center
        StackPanel.verticalAlignment VerticalAlignment.Center
        StackPanel.width 450.
        StackPanel.margin 10.
        StackPanel.children  [
            let v = validateAcceptOrSignMsg(s.AcceptOrSignMsg)
            let localNameV = if s.LocalName |> String.IsNullOrWhiteSpace then Some ("You must specify localName") else None
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.name "LocalName"
                TextBox.watermark "Type the local name of the DLC you want to checksig"
                TextBox.errors (localNameV |> Option.toList |> Seq.cast<obj>)
                TextBox.text (s.LocalName)
                yield! TextBox.onTextInput(UpdateLocalName >> ForSelf >> dispatch)
            ]
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.name "Base64Accept"
                TextBox.useFloatingWatermark true
                TextBox.watermark (sprintf "Paste Base64-encoded Accept (or Sign) message here")
                TextBox.height 120.
                TextBox.errors (v |> Option.toList |> Seq.cast<obj>)
                TextBox.text (s.AcceptOrSignMsg)
                yield! TextBox.onTextInput(UpdateAcceptOrSignMsg >> ForSelf >> dispatch)
            ]
            StackPanel.create [
                StackPanel.orientation Orientation.Horizontal
                StackPanel.dock Dock.Right
                StackPanel.children [
                    Button.create[
                        Button.classes ["round"]
                        Button.content "Cancel"
                        Button.onClick(fun _ -> Reset |> ForSelf |> dispatch)
                    ]
                    Button.create [
                        Button.isEnabled (v.IsNone && localNameV.IsNone)
                        Button.classes [ "round" ]
                        Button.content "Review"
                        Button.onClick(fun _ -> CommitAcceptOrSignMsg |> ForSelf |> dispatch)
                    ]
                ]
            ]
            TextBlock.create [
                TextBlock.classes ["error"]
                TextBlock.text (s.ErrorMsg |> Option.toObj)
                TextBlock.isVisible(s.ErrorMsg.IsSome)
                TextBlock.textWrapping TextWrapping.Wrap
                TextBlock.height 150.
            ]
        ]
    ]
    
