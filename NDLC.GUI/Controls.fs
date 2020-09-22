namespace NDLC.GUI

open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.Input

module TextBox =
    let onTextInput handler =
        [
            TextBox.onKeyDown(fun args -> handler (args.Source :?> TextBox).Text)
            TextBox.onKeyUp(fun args -> handler (args.Source :?> TextBox).Text)
        ]
        
    let onTextInputFinished handler =
        let handler' =
           fun (args: KeyEventArgs) ->
               if args.Key = Key.Enter || args.Key = Key.Tab then
                   handler (args.Source :?> TextBox).Text
        [
            TextBox.onKeyDown(handler')
            TextBox.onKeyUp(handler')
        ]
        
        
module StackPanel =
    let onTextboxInput handler =
        [
            TextBox.onKeyDown(fun args -> handler (args.Source :?> TextBox))
            TextBox.onKeyUp(fun args -> handler (args.Source :?> TextBox))
        ]
        
