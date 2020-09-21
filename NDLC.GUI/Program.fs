namespace NDLC.GUI

open System
open Elmish
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Input
open Avalonia.FuncUI
open Avalonia.FuncUI.Elmish
open Avalonia.FuncUI.Components.Hosts
open Avalonia.Platform
open NDLC.GUI.Utils

type MainWindow() as this =
    inherit HostWindow()
    do
        base.Title <- "NDLC.GUI"
        // base.Width <- 400.0
        // base.Height <- 400.0
        
#if DEBUG
        this.VisualRoot.VisualRoot.Renderer.DrawFps <- true
        this.VisualRoot.VisualRoot.Renderer.DrawDirtyRects <- true
        base.AttachDevTools()
#endif
        
        let c = GlobalConfig.Default
        Elmish.Program.mkProgram (fun () -> Router.init) (Router.update c) Router.view
        |> Program.withHost this
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> Program.run

        
type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Load "avares://Avalonia.Themes.Default/DefaultTheme.xaml"
        this.Styles.Load "avares://Avalonia.Themes.Default/Accents/BaseDark.xaml"
        this.Styles.Load "avares://NDLC.GUI/Styles.xaml"

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            desktopLifetime.MainWindow <- MainWindow()
        | _ -> ()

module Program =
    open Avalonia.Logging.Serilog

    [<EntryPoint>]
    let main(args: string[]) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
#if DEBUG
            .LogToDebug()
#endif
            .StartWithClassicDesktopLifetime(args)