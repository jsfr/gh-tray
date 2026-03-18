namespace GhTray

type CheckStatus =
    | Success
    | Failure
    | Pending

type ReviewStatus =
    | Approved
    | ChangesRequested
    | ReviewRequired

type PullRequest =
    { Title: string
      Url: string
      Number: int
      Repository: string
      IsDraft: bool
      CheckStatus: CheckStatus option
      ReviewStatus: ReviewStatus option
      HasConflicts: bool }

type PullRequestGroup =
    { Mine: PullRequest list
      ReviewRequested: PullRequest list
      Involved: PullRequest list }

module PullRequestGroup =
    let empty =
        { Mine = []
          ReviewRequested = []
          Involved = [] }

    let totalCount (group: PullRequestGroup) =
        List.length group.Mine
        + List.length group.ReviewRequested
        + List.length group.Involved

type PollerOptions = { PollInterval: System.TimeSpan }

type IGitHubClient =
    abstract member GetUsername: unit -> Async<string>
    abstract member FetchPullRequests: username: string -> Async<PullRequestGroup>
