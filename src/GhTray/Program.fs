namespace GhTray

open System
open System.Runtime.InteropServices
open System.Windows.Forms
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

module Program =
    [<DllImport("kernel32.dll", EntryPoint = "AttachConsole")>]
    extern bool private attachConsole(int dwProcessId)

    [<EntryPoint; STAThread>]
    let main _argv =
        attachConsole (-1) |> ignore // attach to parent console for log output

        let authResult = GhCli.validateAuth () |> Async.RunSynchronously

        match authResult with
        | Error err ->
            eprintfn "gh CLI authentication failed: %s" err
            eprintfn "Please run 'gh auth login' first."
            1
        | Ok() ->
            Application.SetColorMode(SystemColorMode.System)
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(false)
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2) |> ignore

            let pollInterval =
                match Environment.GetEnvironmentVariable("GH_TRAY_POLL_INTERVAL") with
                | null -> TimeSpan.FromMinutes(2.0)
                | v ->
                    match Int32.TryParse(v) with
                    | true, seconds -> TimeSpan.FromSeconds(float seconds)
                    | false, _ ->
                        eprintfn "Invalid GH_TRAY_POLL_INTERVAL: %s, using default 120s" v
                        TimeSpan.FromMinutes(2.0)

            let logLevel =
                match Environment.GetEnvironmentVariable("GH_TRAY_LOG_LEVEL") with
                | null -> LogLevel.Information
                | v ->
                    match Enum.TryParse<LogLevel>(v, true) with
                    | true, level -> level
                    | false, _ ->
                        eprintfn "Invalid GH_TRAY_LOG_LEVEL: %s, using default Warning" v
                        LogLevel.Warning

            let host =
                Host
                    .CreateDefaultBuilder()
                    .ConfigureLogging(fun logging ->
                        logging.ClearProviders() |> ignore
                        logging.AddConsole() |> ignore
                        logging.SetMinimumLevel(logLevel) |> ignore)
                    .ConfigureServices(fun services ->
                        services.AddSingleton<IGitHubClient>(GhCliClient()) |> ignore
                        services.AddSingleton<TrayApp>() |> ignore
                        services.AddSingleton<PollerOptions>({ PollInterval = pollInterval }) |> ignore
                        services.AddSingleton<PullRequestPoller>() |> ignore

                        services.AddHostedService<PullRequestPoller>(fun sp ->
                            sp.GetRequiredService<PullRequestPoller>())
                        |> ignore)
                    .Build()

            let lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>()

            lifetime.ApplicationStopping.Register(fun () -> Application.ExitThread())
            |> ignore

            let tray = host.Services.GetRequiredService<TrayApp>()
            tray.SetLoading()

            let hotkeyBinding =
                match Environment.GetEnvironmentVariable("GH_TRAY_HOTKEY") with
                | null -> "Ctrl+Alt+Shift+G"
                | v -> v

            let globalHotKey =
                match HotKeyParser.tryParse hotkeyBinding with
                | Some(modifiers, key) ->
                    let hk = new GlobalHotKey(modifiers, key, tray.ShowMenuAtCursor)

                    if hk.IsRegistered then
                        eprintfn "Global hotkey registered: %s" hotkeyBinding
                        Some(hk :> IDisposable)
                    else
                        eprintfn "Failed to register global hotkey: %s (already in use?)" hotkeyBinding
                        (hk :> IDisposable).Dispose()
                        None
                | None ->
                    eprintfn "Invalid hotkey binding: %s" hotkeyBinding
                    None

            let hostTask = host.RunAsync()

            Application.Run()

            hostTask.GetAwaiter().GetResult()

            globalHotKey |> Option.iter (fun hk -> hk.Dispose())
            (tray :> IDisposable).Dispose()
            0
