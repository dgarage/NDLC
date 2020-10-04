module NDLC.GUI.Oracle.OracleInImportModule

open Avalonia.FuncUI.DSL
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
let view (state) dispatch = StackPanel.create []
