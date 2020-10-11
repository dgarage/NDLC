[<RequireQualifiedAccess>]
module NDLC.GUI.DLCStartModule

open Avalonia
open System
open System.Text
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.FuncUI.DSL
open Avalonia.Media
open FSharp.Control.Tasks
open Elmish
open NBitcoin
open NBitcoin.DataEncoders
open NDLC.GUI.DLC
open NDLC.GUI.Utils
open NDLC.Infrastructure
open NDLC.Messages
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open ResultUtils


type ResultForInitiator = {
    Msg: string
    SignBase64: string
    SignJson: string
}

type ResultForAcceptor = {
    Msg: string
    FinalizedTxHex: string
    FinalizedTxJson: string
}

type private AfterCheckSigState = {
    LocalName: string
    PSBTToShowUser: PSBT
    SignedPSBT: string
}

type StartResult =
    | ForInitiator of ResultForInitiator
    | ForAcceptor of ResultForAcceptor
    
type InternalMsg =
    | InvalidInput of string
    | Reset
    | CopyToClipBoard of string
    | NoOp
    
    | UpdateLocalName of string
    | UpdateSignedPSBT of string
    | ConfirmPSBT
    | FinishOperation of StartResult
    
type OutMsg =
    | Finished of StartResult
    
type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg

type State = {
    ErrorMsg: string option
    LocalName: string
    SignedPSBT: string
}

type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
    OnFinished: StartResult -> 'Msg
}

type Translator<'Msg> = Msg -> 'Msg

let translator ({ OnInternalMsg = onInternalMsg; OnFinished = onInputFinished }: TranslationDictionary<'Msg>): Translator<'Msg> =
    function
    | ForSelf i -> onInternalMsg i
    | ForParent(Finished msg) -> onInputFinished msg
   
let init = {
    ErrorMsg = None
    LocalName = ""
    SignedPSBT = ""
}

let update globalConfig msg (state: State)=
     // -- after checksig --
    match msg with
    | Reset ->
        init, Cmd.none
    | NoOp ->
        state, Cmd.none
    | CopyToClipBoard s ->
        let copy (str) = task {
            do! Application.Current.Clipboard.SetTextAsync str
            return NoOp |> ForSelf
        }
        state, Cmd.OfTask.result (copy s)
    | InvalidInput msg ->
        { state with ErrorMsg = Some msg }, Cmd.none
    | UpdateSignedPSBT psbt ->
        { state with SignedPSBT = psbt }, Cmd.none
    | ConfirmPSBT ->
        // returns psbt or tx
        let start g (name: string, psbtStr: string) = task {
            let! dlc = CommandBase.getDLC g (name)
            let isNullBuilderState =
                dlc |> Option.ofObj |> Option.map(fun x -> x.BuilderState |> isNull) |> Option.defaultValue true
            let isNullOracleInfo =
                dlc |> Option.ofObj |> Option.map(fun x -> x.OracleInfo |> isNull) |> Option.defaultValue true
            if (isNullBuilderState || isNullOracleInfo) then
                return failwith ("This DLC is not in write state")
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
                    Encoders.Base64.EncodeData(UTF8Encoding.UTF8.GetBytes(jSignString))
                return ForInitiator({ Msg = "Finished Signing our part of Funding TX! Send base64 encoded `sign` message to the peer and wait for them to broadcast!"
                                      SignBase64 = base64Sign; SignJson = jSignString  })
        }
        let onSuccess = FinishOperation >> ForSelf
        let onFailure (e: exn) = e.ToString() |> InvalidInput |> ForSelf
        state, Cmd.OfTask.either (start globalConfig) (state.LocalName, state.SignedPSBT) (onSuccess) (onFailure)
    | FinishOperation result ->
        { state with ErrorMsg = None } ,
        Cmd.batch[ Cmd.ofMsg(Finished result |> ForParent); Cmd.ofMsg (Reset |> ForSelf)]
    | UpdateLocalName msg ->
        { state with LocalName = msg }, Cmd.none
        
let view g (s: State) dispatch =
    StackPanel.create [
        StackPanel.horizontalAlignment HorizontalAlignment.Center
        StackPanel.verticalAlignment VerticalAlignment.Center
        StackPanel.width 450.
        StackPanel.margin 10.
        StackPanel.children [
            let v = validatePSBT(s.SignedPSBT, g.Network)
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
                TextBox.name "SignedPSBT"
                TextBox.useFloatingWatermark true
                TextBox.watermark (sprintf "Paste Base64-encoded PSBT after you've signed here")
                TextBox.height 120.
                TextBox.errors (v |> Option.toList |> Seq.cast<obj>)
                TextBox.text (s.SignedPSBT)
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
                    Button.create [
                        Button.isEnabled (v.IsNone)
                        Button.classes [ "round" ]
                        Button.content "Review"
                        Button.onClick(fun _ -> ConfirmPSBT |> ForSelf |> dispatch)
                    ]
                ]
            ]
            TextBlock.create [
                TextBlock.textWrapping TextWrapping.Wrap
                TextBlock.classes ["error"]
                TextBlock.text (s.ErrorMsg |> Option.toObj)
                TextBlock.isVisible(s.ErrorMsg.IsSome)
                TextBlock.height 150.
            ]
        ]
    ]
