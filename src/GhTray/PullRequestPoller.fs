namespace GhTray

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type PullRequestPoller
    (
        client: IGitHubClient,
        tray: TrayApp,
        logger: ILogger<PullRequestPoller>,
        options: PollerOptions
    ) =

    let pollInterval = options.PollInterval
    let mutable username = ""

    interface IHostedService with
        member _.StartAsync(ct: CancellationToken) =
            task {
                let! name = client.GetUsername() |> Async.StartAsTask
                username <- name
                logger.LogInformation("Polling PRs for user: {Username}", username)

                let _ =
                    Task.Run(
                        Func<Task>(fun () ->
                            task {
                                while not ct.IsCancellationRequested do
                                    try
                                        let! group =
                                            client.FetchPullRequests(username) |> Async.StartAsTask

                                        let count = PullRequestGroup.totalCount group
                                        logger.LogDebug("Fetched {Count} PRs", count)
                                        tray.Update(group, pollInterval)
                                    with ex ->
                                        logger.LogError(ex, "Failed to fetch PRs")
                                        tray.MarkStale()

                                    do!
                                        Task.Delay(pollInterval, ct)
                                            .ContinueWith(fun _ -> ())
                            }),
                        ct
                    )

                return ()
            }

        member _.StopAsync(_ct: CancellationToken) =
            logger.LogInformation("Stopping PR poller")
            Task.CompletedTask
