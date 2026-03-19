namespace GhTray

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type PullRequestPoller(client: IGitHubClient, tray: TrayApp, logger: ILogger<PullRequestPoller>, options: PollerOptions)
    =

    let pollInterval = options.PollInterval
    let mutable username = ""

    interface IHostedService with
        member _.StartAsync(ct: CancellationToken) = task {
            eprintfn "[gh-tray] Starting PR poller, fetching username..."
            let! name = client.GetUsername() |> Async.StartAsTask
            username <- name
            eprintfn "[gh-tray] Polling PRs for user: %s" username

            Task.Run(
                Func<Task>(fun () -> task {
                    while not ct.IsCancellationRequested do
                        try
                            eprintfn "[gh-tray] Fetching PRs..."
                            let! group = client.FetchPullRequests(username) |> Async.StartAsTask

                            let count = PullRequestGroup.totalCount group
                            eprintfn "[gh-tray] Fetched %d PRs" count
                            tray.Update(group, pollInterval)
                        with ex ->
                            eprintfn "[gh-tray] Failed to fetch PRs: %s" ex.Message
                            tray.MarkStale()

                        do! Task.Delay(pollInterval, ct).ContinueWith(fun _ -> ())
                }),
                ct
            )
            |> ignore

            return ()
        }

        member _.StopAsync(_ct: CancellationToken) =
            logger.LogInformation("Stopping PR poller")
            Task.CompletedTask
