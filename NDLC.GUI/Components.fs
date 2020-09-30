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


    let inline importAndGenerateButton (onImportClicked) (onGenerateClicked) =
        let button (name: string) (classes: _ list) onClick = 
            Button.create [
                Button.dock Dock.Left
                Button.content name
                Button.margin 4.
                Button.fontSize 15.
                Button.classes classes
                Button.onClick(onClick, SubPatchOptions.Always)
            ]
        StackPanel.create [
            StackPanel.dock Dock.Bottom
            StackPanel.orientation Orientation.Horizontal
            StackPanel.children[
                button "Import" ["round"] onImportClicked
                button "Generate" ["round"; "add"] onGenerateClicked
            ]
        ]
        
    let inputTextBox (name) (watermark) =
        TextBox.create []
