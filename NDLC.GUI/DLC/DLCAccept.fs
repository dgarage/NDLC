module NDLC.GUI.DLCAcceptModule

open Avalonia.FuncUI.DSL


type State = {
    Nil: bool
}

let init = { Nil = false }


let view = StackPanel.create[]
