namespace GhTray

#nowarn "3261"

open System
open System.Collections.Generic
open System.Diagnostics
open System.Text.Json

module GhCli =
    let runGh (args: string) : Async<Result<string, string>> =
        async {
            let psi = ProcessStartInfo("gh", args)
            psi.RedirectStandardOutput <- true
            psi.RedirectStandardError <- true
            psi.UseShellExecute <- false
            psi.CreateNoWindow <- true

            use proc = Process.Start(psi)
            let! stdout = proc.StandardOutput.ReadToEndAsync() |> Async.AwaitTask
            let! stderr = proc.StandardError.ReadToEndAsync() |> Async.AwaitTask
            do! proc.WaitForExitAsync() |> Async.AwaitTask

            if proc.ExitCode = 0 then
                return Ok(stdout.Trim())
            else
                return Error(stderr.Trim())
        }

    let validateAuth () : Async<Result<unit, string>> =
        async {
            match! runGh "auth status" with
            | Ok _ -> return Ok()
            | Error err -> return Error err
        }

module GraphQL =
    let internal query =
        """
        query($searchQuery: String!) {
          search(query: $searchQuery, type: ISSUE, first: 100) {
            nodes {
              ... on PullRequest {
                title
                url
                number
                isDraft
                repository { nameWithOwner }
                author { login }
                reviewRequests(first: 10) {
                  nodes {
                    requestedReviewer {
                      ... on User { login }
                    }
                  }
                }
                reviews(last: 1, states: [APPROVED, CHANGES_REQUESTED]) {
                  nodes { state }
                }
                commits(last: 1) {
                  nodes {
                    commit {
                      statusCheckRollup { state }
                    }
                  }
                }
                mergeable
              }
            }
          }
        }
        """

    let private parseCheckStatus (commitNodes: JsonElement) : CheckStatus option =
        try
            let node = commitNodes.EnumerateArray() |> Seq.tryHead

            match node with
            | None -> None
            | Some n ->
                let rollup = n.GetProperty("commit").GetProperty("statusCheckRollup")

                if rollup.ValueKind = JsonValueKind.Null then
                    None
                else
                    match rollup.GetProperty("state").GetString() with
                    | "SUCCESS" -> Some Success
                    | "FAILURE" | "ERROR" -> Some Failure
                    | _ -> Some Pending
        with :? KeyNotFoundException ->
            None

    let private parseReviewStatus (reviewNodes: JsonElement) : ReviewStatus option =
        try
            let node = reviewNodes.EnumerateArray() |> Seq.tryHead

            match node with
            | None -> None
            | Some n ->
                match n.GetProperty("state").GetString() with
                | "APPROVED" -> Some Approved
                | "CHANGES_REQUESTED" -> Some ChangesRequested
                | _ -> None
        with :? KeyNotFoundException ->
            None

    let private parseMergeable (value: string) : bool =
        value = "CONFLICTING"

    let private parseReviewerLogins (reviewRequests: JsonElement) : string list =
        try
            [ for node in reviewRequests.EnumerateArray() do
                  let reviewer = node.GetProperty("requestedReviewer")

                  if reviewer.ValueKind <> JsonValueKind.Null then
                      match reviewer.TryGetProperty("login") with
                      | true, login -> login.GetString()
                      | _ -> () ]
        with :? KeyNotFoundException ->
            []

    let private parsePullRequest (node: JsonElement) : PullRequest option =
        try
            let title = node.GetProperty("title").GetString()

            if isNull title then
                None
            else
                Some
                    { Title = title
                      Url = node.GetProperty("url").GetString()
                      Number = node.GetProperty("number").GetInt32()
                      Repository = node.GetProperty("repository").GetProperty("nameWithOwner").GetString()
                      IsDraft = node.GetProperty("isDraft").GetBoolean()
                      CheckStatus =
                        parseCheckStatus (node.GetProperty("commits").GetProperty("nodes"))
                      ReviewStatus =
                        parseReviewStatus (node.GetProperty("reviews").GetProperty("nodes"))
                      HasConflicts =
                        parseMergeable (node.GetProperty("mergeable").GetString()) }
        with
        | :? KeyNotFoundException -> None
        | :? InvalidOperationException -> None

    let classifyPullRequests (username: string) (nodes: JsonElement) : PullRequestGroup =
        let mutable mine = []
        let mutable reviewRequested = []
        let mutable involved = []

        for node in nodes.EnumerateArray() do
            match parsePullRequest node with
            | None -> ()
            | Some pr ->
                let authorLogin =
                    try
                        node.GetProperty("author").GetProperty("login").GetString()
                    with :? KeyNotFoundException ->
                        ""

                let reviewerLogins =
                    try
                        parseReviewerLogins (
                            node.GetProperty("reviewRequests").GetProperty("nodes")
                        )
                    with :? KeyNotFoundException ->
                        []

                if String.Equals(authorLogin, username, StringComparison.OrdinalIgnoreCase) then
                    mine <- pr :: mine
                elif reviewerLogins
                     |> List.exists (fun l ->
                         String.Equals(l, username, StringComparison.OrdinalIgnoreCase)) then
                    reviewRequested <- pr :: reviewRequested
                else
                    involved <- pr :: involved

        { Mine = List.rev mine
          ReviewRequested = List.rev reviewRequested
          Involved = List.rev involved }

type GhCliClient() =
    interface IGitHubClient with
        member _.GetUsername() =
            async {
                match! GhCli.runGh "api user --jq .login" with
                | Ok username -> return username
                | Error err -> return failwith $"Failed to get GitHub username: {err}"
            }

        member _.FetchPullRequests(username: string) =
            async {
                let searchQuery =
                    $"sort:updated-desc type:pr state:open involves:{username}"

                let args =
                    $"""api graphql -f query='{GraphQL.query}' -f searchQuery='{searchQuery}'"""

                match! GhCli.runGh args with
                | Ok json ->
                    let doc = JsonDocument.Parse(json)
                    let nodes = doc.RootElement.GetProperty("data").GetProperty("search").GetProperty("nodes")
                    return GraphQL.classifyPullRequests username nodes
                | Error err -> return failwith $"GitHub GraphQL query failed: {err}"
            }
