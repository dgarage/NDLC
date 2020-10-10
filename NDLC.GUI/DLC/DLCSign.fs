[<RequireQualifiedAccess>]
module NDLC.GUI.DLCSignModule

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

type private BeforeCheckSigState = {
    LocalName: string
    AcceptMsg: string
}

type private AfterCheckSigState = {
    LocalName: string
    PSBTToShowUser: PSBT
    SignedPSBT: string
}

type SignResultForInitiator = {
    Msg: string
    SignBase64: string
    SignJson: string
}

type SignResultForAcceptor = {
    Msg: string
    FinalizedTxHex: string
    FinalizedTxJson: string
}

type SignResult =
    | ForInitiator of SignResultForInitiator
    | ForAcceptor of SignResultForAcceptor

type StepState =
    private
    | BeforeCheckSig of BeforeCheckSigState
    | AfterCheckSig of AfterCheckSigState

type State = {
    ErrorMsg: string option
    Step: StepState
}

type InternalMsg =
    // Step agnostic
    | InvalidInput of string
    | Reset
    | CopyToClipBoard of string
    | NoOp
    
    // BeforeCheckSig
    | UpdateAcceptMsg of string
    | UpdateLocalName of string
    | CommitAcceptMsg
    | CheckSigFinished of psbt: PSBT
    
    // AfterCheckSig
    | UpdateSignedPSBT of string
    | ConfirmPSBT
    | FinishOperation of SignResult

type OutMsg =
    | Finished of SignResult
type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg
type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
    OnInputFinished: SignResult -> 'Msg
}

type Translator<'Msg> = Msg -> 'Msg

let translator ({ OnInternalMsg = onInternalMsg; OnInputFinished = onInputFinished }: TranslationDictionary<'Msg>): Translator<'Msg> =
    function
    | ForSelf i -> onInternalMsg i
    | ForParent(Finished msg) -> onInputFinished msg
    
let init = {
    ErrorMsg = None
    Step = BeforeCheckSig { AcceptMsg = ""; LocalName = "" }
}

[<AutoOpen>]
module private Tasks =
    let validateAcceptMsg (accept: string): Option<_> =
        if (accept |> String.IsNullOrWhiteSpace) then Some "Empty Offer msg not allowed" else
        if (accept |> String.isBase64 |> not) then Some "Offer Msg must be base64 encoded" else
        None
        
    let validatePSBTSigned g (s: string): Option<_> =
        match tryParsePSBT(s, g.Network) with
        | Error e -> Some (e)
        | Ok psbt ->
            failwith "k"

let update globalConfig msg state =
    match msg, state.Step with
    | Reset, _ ->
        init, Cmd.none
    | InvalidInput msg, _ ->
        { state with ErrorMsg = Some msg }, Cmd.none
    | CopyToClipBoard s, _ ->
        let copy (str) = task {
            do! Application.Current.Clipboard.SetTextAsync str
            return NoOp |> ForSelf
        }
        state, Cmd.OfTask.result (copy s)
        
     // -- before checksig --
    | UpdateAcceptMsg msg, BeforeCheckSig s ->
        { state with Step = BeforeCheckSig { s with AcceptMsg = msg }}, Cmd.none
    | UpdateLocalName msg, BeforeCheckSig s ->
        { state with Step = BeforeCheckSig { s with LocalName = msg }}, Cmd.none
    | CommitAcceptMsg, BeforeCheckSig s ->
        let onSuccess = CheckSigFinished >> ForSelf
        let onFailure (e: exn) = InvalidInput (e.Message) |> ForSelf
        state, Cmd.OfTask.either (DLC.CheckSig.checksig globalConfig) (s.AcceptMsg) (onSuccess) (onFailure)
    | CheckSigFinished psbt, BeforeCheckSig s  ->
        { state with Step = AfterCheckSig { PSBTToShowUser = psbt; SignedPSBT = ""; LocalName = s.LocalName }; ErrorMsg = None }, Cmd.none
        
     // -- after checksig --
    | UpdateSignedPSBT psbt, AfterCheckSig s ->
        { state with Step = AfterCheckSig { s with SignedPSBT = psbt } }, Cmd.none
    | ConfirmPSBT, AfterCheckSig s ->
        // returns psbt or tx
        let start g (name: string, psbtStr: string) = task {
            let! dlc = CommandBase.getDLC g (name)
            let isNullBuilderState =
                dlc |> Option.ofObj |> Option.map(fun x -> x.BuilderState |> isNull) |> Option.defaultValue true
            let isNullOracleInfo =
                dlc |> Option.ofObj |> Option.map(fun x -> x.OracleInfo |> isNull) |> Option.defaultValue true
            if (isNullBuilderState || isNullOracleInfo) then
                return failwith ("This DLC does not exist")
            else
            let builder = DLCTransactionBuilder(dlc.BuilderState.ToString(), g.Network)
            let psbt = tryParsePSBT(psbtStr, g.Network) |> Result.deref
            let repo = ConfigUtils.repository g
            if (not <| builder.State.IsInitiator) then
                DLCUtils.assertState(dlc, false, Repository.DLCState.DLCNextStep.Fund, g.Network) |> Result.deref
                let fullySigned = builder.Finalize(psbt);
                dlc.BuilderState <- builder.ExportStateJObject()
                do! repo.SaveDLC(dlc)
                return ForAcceptor { Msg = "Funding TX is fully ready! You must now broadcast the transaction"
                                     FinalizedTxHex = fullySigned.ToHex(); FinalizedTxJson = fullySigned.ToString() }
            else
                DLCUtils.assertState(dlc, true, Repository.DLCState.DLCNextStep.Fund, g.Network) |> Result.deref
                let! key = repo.GetKey(dlc.FundKeyPath);
                let sign = builder.Sign2(key, psbt);
                dlc.Sign <- JObject.FromObject(sign, JsonSerializer.Create(repo.JsonSettings));
                dlc.BuilderState <- builder.ExportStateJObject();
                do! repo.SaveDLC(dlc);
                let jSignString = JsonConvert.SerializeObject(sign, repo.JsonSettings);
                let base64Sign =
                    UTF8Encoding.UTF8.GetString(Encoders.Base64.DecodeData(jSignString))
                return ForInitiator({ Msg = "Finished Signing our part of Funding TX! Send base64 encoded `sign` message to the peer and wait for them to broadcast!"
                                      SignBase64 = base64Sign; SignJson = jSignString  })
        }
        let onSuccess = FinishOperation >> ForSelf
        let onFailure (e: exn) = e.Message |> InvalidInput |> ForSelf
        state, Cmd.OfTask.either (start globalConfig) (s.LocalName, s.SignedPSBT) (onSuccess) (onFailure)
    | FinishOperation result, AfterCheckSig _ ->
        { state with ErrorMsg = None} ,
        Cmd.batch[ Cmd.ofMsg(Finished result |> ForParent); Cmd.ofMsg (Reset |> ForSelf)]
    | _ ->
        Debug.Assert(false, "Unreachable!")
        state, Cmd.none
        
let private checkSigView (s: BeforeCheckSigState) dispatch =
    StackPanel.create [
        StackPanel.children  [
            let v = validateAcceptMsg(s.AcceptMsg)
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
                TextBox.watermark (sprintf "Paste Base64-encoded accept message here")
                TextBox.height 120.
                TextBox.errors (v |> Option.toList |> Seq.cast<obj>)
                TextBox.text (s.AcceptMsg)
                yield! TextBox.onTextInput(UpdateAcceptMsg >> ForSelf >> dispatch)
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
                        Button.onClick(fun _ -> CommitAcceptMsg |> ForSelf |> dispatch)
                    ]
                ]
            ]
        ]
    ]
    
let private psbtView globalConfig (s: AfterCheckSigState) dispatch =
    StackPanel.create [
        StackPanel.children [
            TextBlock.create [
                TextBlock.text "Please Copy the following PSBT and sign it with your wallet"
            ]
            TextBox.create [
                TextBox.isEnabled false
                TextBox.text (s.PSBTToShowUser.ToBase64())
                TextBox.contextMenu (ContextMenu.create [
                    ContextMenu.viewItems [
                        MenuItem.create [
                            MenuItem.header "Copy Base64"
                            MenuItem.onClick(fun (_) -> s.PSBTToShowUser.ToBase64() |> CopyToClipBoard |> ForSelf |> dispatch)
                        ]
                    ]
                ])
            ]
            let maybePSBTError = validatePSBTSigned globalConfig (s.SignedPSBT)
            TextBox.create [
                TextBox.classes ["userinput"]
                TextBox.name "SignedPSBT"
                TextBox.text (s.SignedPSBT)
                TextBox.useFloatingWatermark true
                TextBox.watermark "Paste signed PSBT here"
                TextBox.errors (maybePSBTError |> Option.toList |> Seq.cast<obj>)
                yield! TextBox.onTextInput(UpdateSignedPSBT >> ForSelf >> dispatch)
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
                    Button.create[
                        Button.classes ["round"]
                        Button.content "Confirm"
                        Button.isEnabled(maybePSBTError.IsNone)
                        Button.onClick(fun _ -> ConfirmPSBT |> ForSelf |> dispatch)
                    ]
                ]
            ]
        ]
    ]
    
let view g (state: State) dispatch =
    StackPanel.create [
        StackPanel.horizontalAlignment HorizontalAlignment.Center
        StackPanel.verticalAlignment VerticalAlignment.Center
        StackPanel.width 450.
        StackPanel.margin 10.
        StackPanel.children [
            match state.Step with
            | BeforeCheckSig s ->
                (checkSigView s dispatch)
            | AfterCheckSig s ->
                (psbtView g s dispatch)
            TextBlock.create [
                TextBlock.classes ["error"]
                TextBlock.text (state.ErrorMsg |> Option.toObj)
                TextBlock.isVisible(state.ErrorMsg.IsSome)
            ]
        ]
    ]
