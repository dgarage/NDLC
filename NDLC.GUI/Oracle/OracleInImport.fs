module NDLC.GUI.Oracle.OracleInImportModule

open Avalonia.FuncUI.DSL
open Elmish
open NDLC.Messages

type InternalMsg =
    | UpdateFoo

type OutMsg =
    | Foo
type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg

type State = {
    Nil: bool
    ImportingOracle: OracleInfo option
}
type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
}

type Translator<'Msg> = Msg -> 'Msg

let init =
    { State.Nil = false; ImportingOracle = None }

let translator ({ OnInternalMsg = onInternalMsg; }: TranslationDictionary<'Msg>): Translator<'Msg> =
    function
    | ForSelf i -> onInternalMsg i
    
let update (msg: InternalMsg) (state: State) =
    match msg with
    | UpdateFoo  -> state, Cmd.none
let view (state) dispatch = StackPanel.create []
