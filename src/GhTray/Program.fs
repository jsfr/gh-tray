namespace GhTray

open System
open System.Runtime.InteropServices
open System.Windows.Forms
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

module Program =
    [<DllImport("kernel32.dll", EntryPoint = "AttachConsole")>]
    extern bool private attachConsole(int _dwProcessId)

    [<EntryPoint; STAThread>]
    let main argv =
        attachConsole -1 |> ignore // attach to parent console for log output

        let demoMode = argv |> Array.contains "--demo"

        let config = Config.load ()

        let ghToken =
            if demoMode then
                None
            else
                match config.Account with
                | Some account ->
                    match
                        GhCli.runGh None [ "auth"; "token"; "--user"; account ]
                        |> Async.RunSynchronously
                    with
                    | Ok token -> Some token
                    | Error err ->
                        eprintfn "Failed to get token for account '%s': %s" account err
                        None
                | None -> None

        if not demoMode then
            let authResult = GhCli.validateAuth ghToken |> Async.RunSynchronously

            match authResult with
            | Error err ->
                eprintfn "gh CLI authentication failed: %s" err
                eprintfn "Please run 'gh auth login' first."
                Environment.Exit 1
            | Ok() -> ()

        Application.SetColorMode SystemColorMode.System
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault false
        Application.SetHighDpiMode HighDpiMode.PerMonitorV2 |> ignore

        let pollInterval =
            match Environment.GetEnvironmentVariable "GH_TRAY_POLL_INTERVAL" with
            | null -> config.PollInterval
            | v ->
                match Int32.TryParse v with
                | true, seconds -> seconds
                | false, _ ->
                    eprintfn "Invalid GH_TRAY_POLL_INTERVAL: %s, using default %ds" v config.PollInterval
                    config.PollInterval
            |> float
            |> TimeSpan.FromSeconds

        let parseLogLevel (v: string) =
            match Enum.TryParse<LogLevel>(v, true) with
            | true, level -> level
            | false, _ ->
                eprintfn "Invalid log level: %s, using default %s" v config.LogLevel
                LogLevel.Information

        let logLevel =
            match Environment.GetEnvironmentVariable "GH_TRAY_LOG_LEVEL" with
            | null -> parseLogLevel config.LogLevel
            | v -> parseLogLevel v

        let ghClient: IGitHubClient = if demoMode then DemoClient() else GhCliClient ghToken

        let host =
            Host
                .CreateDefaultBuilder()
                .ConfigureLogging(fun logging ->
                    logging.ClearProviders() |> ignore
                    logging.AddConsole() |> ignore

                    match config.LogFile with
                    | Some path -> logging.AddProvider(new FileLoggerProvider(path, logLevel)) |> ignore
                    | None -> ()

                    logging.SetMinimumLevel logLevel |> ignore)
                .ConfigureServices(fun services ->
                    services.AddSingleton<IGitHubClient>(ghClient) |> ignore
                    services.AddSingleton<TrayApp>() |> ignore
                    services.AddSingleton<PollerOptions> { PollInterval = pollInterval } |> ignore
                    services.AddSingleton<PullRequestPoller>() |> ignore

                    services.AddHostedService<PullRequestPoller>(fun sp -> sp.GetRequiredService<PullRequestPoller>())
                    |> ignore)
                .Build()

        let lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>()

        lifetime.ApplicationStopping.Register(fun () -> Application.ExitThread())
        |> ignore

        let tray = host.Services.GetRequiredService<TrayApp>()
        tray.SetLoading()

        let hotkeyBinding =
            match Environment.GetEnvironmentVariable "GH_TRAY_HOTKEY" with
            | null -> config.Hotkey
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
