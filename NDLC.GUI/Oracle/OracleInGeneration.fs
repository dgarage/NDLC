module NDLC.GUI.Oracle.OracleInGenerationModule

open Avalonia.FuncUI.DSL

type InternalMsg =
    | UpdateFoo

type OutMsg =
    | Foo
type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg

type State = {
    Nil: bool
}
type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
}

type Translator<'Msg> = Msg -> 'Msg

let init =
    { State.Nil = false }

let translator ({ OnInternalMsg = onInternalMsg; }: TranslationDictionary<'Msg>): Translator<'Msg> =
    function
    | ForSelf i -> onInternalMsg i

let view (state) dispatch = StackPanel.create []
