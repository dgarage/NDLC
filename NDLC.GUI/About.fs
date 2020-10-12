namespace NDLC.GUI

open System
open System.Diagnostics
open System.Runtime.InteropServices

open Avalonia.Layout
open Avalonia.Controls
open Avalonia.Controls.Presenters
open Avalonia.Media
open Avalonia.Styling

open Elmish
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Components
open Avalonia.FuncUI.Elmish


module About =
    type State = { noop: bool }
    
    type Links =
        | NDLC
        | DLCSpecs
        | AvaloniaFuncUI
        with
        member this.Text =
            match this with
            | NDLC -> "About NDLC"
            | DLCSpecs -> "Spec for the DLC protocol itself"
            | AvaloniaFuncUI -> "About the framework we used to create this GUI"
    
    type Msg = OpenUrl of Links
    
    let init = { noop = false }
    
    
    let update (msg: Msg) (state: State) =
        match msg with
        | OpenUrl link ->
            let url =
                match link with
                | NDLC -> "https://github.com/dgarage/NDLC"
                | DLCSpecs -> "https://github.com/discreetlogcontracts/dlcspecs"
                | AvaloniaFuncUI -> "https://github.com/AvaloniaCommunity/Avalonia.FuncUI"
                
            ignore <|
                if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                    let start = sprintf "/c start %s" url
                    Process.Start(ProcessStartInfo("cmd", start))
                else if RuntimeInformation.IsOSPlatform OSPlatform.Linux then
                    Process.Start ("xdg-open", url)
                else if RuntimeInformation.IsOSPlatform OSPlatform.OSX then
                    Process.Start ("open", url)
                else
                    raise <| NotSupportedException("Unknown OS platform")
            state


    let view (state: State) (dispatch: Msg -> unit) =
        let linkView (l: Links) =
            TextBlock.create [
                TextBlock.classes ["link"]
                TextBlock.text l.Text
                TextBlock.foreground "#009bd2"
                TextBlock.fontSize 16.
                TextBlock.fontWeight FontWeight.SemiBold
                TextBlock.fontStyle FontStyle.Oblique
                TextBlock.onTapped (fun _ -> dispatch (OpenUrl l))
            ]
        DockPanel.create [
            DockPanel.margin (0., 20.)
            DockPanel.horizontalAlignment HorizontalAlignment.Center
            DockPanel.verticalAlignment VerticalAlignment.Top
            DockPanel.children [
                StackPanel.create [
                    StackPanel.dock Dock.Top
                    StackPanel.verticalAlignment VerticalAlignment.Top
                    StackPanel.children [
                        TextBlock.create [
                            TextBlock.classes ["title"]
                            TextBlock.fontSize 24.
                            TextBlock.fontWeight FontWeight.Thin
                            TextBlock.text "GUI application for managing DLC (Experimental)"
                        ]
                        TextBlock.create [
                            TextBlock.classes [ "subtitle" ]
                            TextBlock.fontSize 16.
                            TextBlock.fontWeight FontWeight.Thin
                            TextBlock.text
                                ("DLC is an exciting new mechanism to create an anonymous prediction market\n" +
                                 "with just a bitcoin. And this is a wrapper GUI to manage your position and more.")
                        ]
                    ]
                ]
                StackPanel.create [
                    StackPanel.dock Dock.Left
                    StackPanel.horizontalAlignment HorizontalAlignment.Left
                    StackPanel.children [
                        linkView NDLC
                        linkView Links.DLCSpecs
                        linkView Links.AvaloniaFuncUI
                    ]
                ]
                StackPanel.create [
                    StackPanel.dock Dock.Right
                ]
            ]
        ]
        
    type Host() as this =
        inherit Hosts.HostControl()
        do
            this.Styles.Load "avares://NDLC.GUI/Styles.xaml"
            Elmish.Program.mkSimple (fun () -> init) update view
            |> Program.withHost this
            |> Program.run
            
    