module NDLC.GUI.DLCSetupModule

open Avalonia.Controls
open Elmish
open Avalonia.FuncUI.DSL

open Avalonia.Layout
open NBitcoin
open NDLC.Infrastructure
open NDLC.Messages

open NDLC.GUI.Utils
open NDLC.GUI


type State = {
    PSBT: PSBT option
    EventFullName: EventFullName
    ResultedOfferMsg: Offer option
    Error: string option
}
    with
    static member Empty = {
        PSBT = None
        EventFullName = EventFullName.Empty
        ResultedOfferMsg = None
        Error = None
    }
    
type InternalMsg =
    | Update of State
    | InvalidInput of string
    
type OutMsg =
    | SetupFinished of Offer
    
type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg
    
type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
    OnSetupFinished: Offer -> 'Msg
}
type Translator<'Msg> = Msg -> 'Msg

let translator ({ OnInternalMsg = onInternalMsg; OnSetupFinished = onSetupFinished}: TranslationDictionary<'Msg>): Translator<'Msg> =
    function
    | ForSelf msg -> onInternalMsg msg
    | ForParent(SetupFinished offer) -> onSetupFinished offer

let init: _ * Cmd<InternalMsg> =
    State.Empty, Cmd.none
    
let update msg state =
    match msg with
    | Update s ->
        s, Cmd.none
    | InvalidInput (msg) ->
       { state with Error = Some msg }, Cmd.none
    

let view (globalConfig: GlobalConfig) (state: State) dispatch =
    StackPanel.create [
        StackPanel.horizontalAlignment HorizontalAlignment.Center
        StackPanel.verticalAlignment VerticalAlignment.Center
        StackPanel.orientation Orientation.Vertical
        StackPanel.width 450.
        StackPanel.margin 10.
        StackPanel.children [
            TextBox.create [
                TextBox.watermark "Paste your setup psbt here"
                let handler (str: string) =
                    match PSBT.TryParse(str.Trim(), globalConfig.Network) with
                    | true, psbt ->
                        Update { state with PSBT = Some psbt } |> ForSelf |> dispatch
                    | _ ->
                        "Failed to parse psbt" |> InvalidInput |> ForSelf |> dispatch
                yield! TextBox.onTextInputFinished(handler)
            ]
        ]
    ]
