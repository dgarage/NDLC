namespace NDLC.GUI
open System

open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.Input
open Avalonia.Interactivity

module TextBox =
    let onTextInput handler =
        let handler' (args: RoutedEventArgs) =
            let tx = (args.Source :?> TextBox).Text
            if String.IsNullOrEmpty tx then () else
            handler tx
        [
            TextBox.onKeyDown(handler')
            TextBox.onKeyUp(handler')
        ]
        
    let onTextInputFinished handler =
        let handler' =
           fun (args: KeyEventArgs) ->
               if args.Key = Key.Enter || args.Key = Key.Tab then
                   let tx = (args.Source :?> TextBox).Text
                   if String.IsNullOrEmpty tx then () else
                   handler tx
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
        
