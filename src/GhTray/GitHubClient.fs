namespace GhTray


open System
open System.Collections.Generic
open System.Diagnostics
open System.Text.Json

module GhCli =
    let runGh (token: string option) (args: string list) : Async<Result<string, string>> = async {
        let psi = ProcessStartInfo "gh"

        for arg in args do
            psi.ArgumentList.Add arg

        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false
        psi.CreateNoWindow <- true

        match token with
        | Some t -> psi.Environment["GH_TOKEN"] <- t
        | None -> ()

        match Process.Start psi with
        | null -> return Error "Failed to start gh process"
        | proc ->
            use proc = proc
            let! stdout = proc.StandardOutput.ReadToEndAsync() |> Async.AwaitTask
            let! stderr = proc.StandardError.ReadToEndAsync() |> Async.AwaitTask
            do! proc.WaitForExitAsync() |> Async.AwaitTask

            if proc.ExitCode = 0 then
                return Ok(stdout.Trim())
            else
                return Error(stderr.Trim())
    }

    let validateAuth (token: string option) : Async<Result<unit, string>> = async {
        match! runGh token [ "auth"; "status" ] with
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
                let rollup = n.GetProperty("commit").GetProperty "statusCheckRollup"

                if rollup.ValueKind = JsonValueKind.Null then
                    None
                else
                    match rollup.GetProperty("state").GetString() |> Option.ofObj with
                    | Some "SUCCESS" -> Some Success
                    | Some "FAILURE"
                    | Some "ERROR" -> Some Failure
                    | Some _ -> Some Pending
                    | None -> None
        with :? KeyNotFoundException ->
            None

    let private parseReviewStatus (reviewNodes: JsonElement) : ReviewStatus option =
        try
            let node = reviewNodes.EnumerateArray() |> Seq.tryHead

            match node with
            | None -> None
            | Some n ->
                match n.GetProperty("state").GetString() |> Option.ofObj with
                | Some "APPROVED" -> Some Approved
                | Some "CHANGES_REQUESTED" -> Some ChangesRequested
                | _ -> None
        with :? KeyNotFoundException ->
            None

    let private parseMergeable (value: string) : bool = value = "CONFLICTING"

    let private parseReviewerLogins (reviewRequests: JsonElement) : string list =
        try
            [ for node in reviewRequests.EnumerateArray() do
                  let reviewer = node.GetProperty "requestedReviewer"

                  if reviewer.ValueKind <> JsonValueKind.Null then
                      match reviewer.TryGetProperty "login" with
                      | true, login ->
                          match login.GetString() |> Option.ofObj with
                          | Some s -> s
                          | None -> ()
                      | _ -> () ]
        with :? KeyNotFoundException ->
            []

    let private parsePullRequest (node: JsonElement) : PullRequest option =
        try
            match node.GetProperty("title").GetString() |> Option.ofObj with
            | None -> None
            | Some title ->
                let url =
                    node.GetProperty("url").GetString() |> Option.ofObj |> Option.defaultValue ""

                let repo =
                    node.GetProperty("repository").GetProperty("nameWithOwner").GetString()
                    |> Option.ofObj
                    |> Option.defaultValue ""

                let mergeable =
                    node.GetProperty("mergeable").GetString()
                    |> Option.ofObj
                    |> Option.defaultValue ""

                Some
                    { Title = title
                      Url = url
                      Number = node.GetProperty("number").GetInt32()
                      Repository = repo
                      IsDraft = node.GetProperty("isDraft").GetBoolean()
                      CheckStatus = parseCheckStatus (node.GetProperty("commits").GetProperty "nodes")
                      ReviewStatus = parseReviewStatus (node.GetProperty("reviews").GetProperty "nodes")
                      HasConflicts = parseMergeable mergeable }
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
                        |> Option.ofObj
                        |> Option.defaultValue ""
                    with :? KeyNotFoundException ->
                        ""

                let reviewerLogins =
                    try
                        parseReviewerLogins (node.GetProperty("reviewRequests").GetProperty "nodes")
                    with :? KeyNotFoundException ->
                        []

                if String.Equals(authorLogin, username, StringComparison.OrdinalIgnoreCase) then
                    mine <- pr :: mine
                elif
                    reviewerLogins
                    |> List.exists (fun l -> String.Equals(l, username, StringComparison.OrdinalIgnoreCase))
                then
                    reviewRequested <- pr :: reviewRequested
                else
                    involved <- pr :: involved

        { Mine = List.rev mine
          ReviewRequested = List.rev reviewRequested
          Involved = List.rev involved }

type GhCliClient(token: string option) =
    interface IGitHubClient with
        member _.GetUsername() = async {
            match! GhCli.runGh token [ "api"; "user"; "--jq"; ".login" ] with
            | Ok username -> return username
            | Error err -> return failwith $"Failed to get GitHub username: {err}"
        }

        member _.FetchPullRequests(username: string) = async {
            let searchQuery = $"sort:updated-desc type:pr state:open involves:%s{username}"

            let args =
                [ "api"
                  "graphql"
                  "-f"
                  $"query=%s{GraphQL.query}"
                  "-f"
                  $"searchQuery=%s{searchQuery}" ]

            match! GhCli.runGh token args with
            | Ok json ->
                let doc = JsonDocument.Parse json

                let nodes =
                    doc.RootElement.GetProperty("data").GetProperty("search").GetProperty "nodes"

                return GraphQL.classifyPullRequests username nodes
            | Error err -> return failwith $"GitHub GraphQL query failed: {err}"
        }
