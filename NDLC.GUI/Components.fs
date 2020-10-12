namespace NDLC.GUI

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Presenters
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.Styling

module Components =
    let spinner =
        StackPanel.create [
            StackPanel.children [
                StackPanel.create [
                    StackPanel.horizontalAlignment HorizontalAlignment.Center
                    StackPanel.verticalAlignment VerticalAlignment.Center
                    StackPanel.children [
                        TextBox.create [
                            TextBox.text "Now loading..."
                        ]
                    ]
                ]
            ]
        ]


    let inputTextBox (name) (watermark) =
        TextBox.create []
