# gh-tray Design Spec

A Windows system tray application that monitors GitHub pull requests. Built with F# and .NET 10, using WinForms `NotifyIcon` for the tray icon. Inspired by the [GHADOPullRequest Hammerspoon Spoon](https://github.com/jsfr/Spoons/blob/main/Source/GHADOPullRequest.spoon/init.fnl), but GitHub-only.

## Requirements

- Show a system tray icon with the total count of open PRs involving the user
- Poll GitHub on a configurable interval (default 60 seconds)
- Display PRs in a context menu grouped into three sections: My PRs, Review Requested, Involved
- Show status indicators per PR: CI status, review status, merge conflicts, draft state
- Click a PR menu item to open its URL in the default browser
- Fail fast on startup if `gh` CLI is not available or not authenticated
- On transient polling errors, keep last known good state and indicate staleness
- Follow 12-factor principles: config via environment, logs to stderr, backing services behind interfaces

## Architecture

Single-project F# WinForms app. No visible window — only a `NotifyIcon` with `ContextMenuStrip`.

### Project Structure

```
gh-tray/
├── src/
│   └── GhTray/
│       ├── GhTray.fsproj
│       ├── Program.fs          # Entry point, DI setup, app lifecycle
│       ├── Types.fs             # Domain types (PR, Status, etc.)
│       ├── GitHubClient.fs      # Interface + gh CLI implementation
│       ├── PullRequestPoller.fs  # Timer-based polling, state management
│       └── TrayIcon.fs          # NotifyIcon + ContextMenuStrip rendering
├── Directory.Build.props
├── Directory.Packages.props
├── nuget.config
├── global.json
├── .editorconfig
└── .gitignore
```

### Domain Types

```fsharp
type CheckStatus = Success | Failure | Pending

type ReviewStatus = Approved | ChangesRequested | ReviewRequired

type PullRequest = {
    Title: string
    Url: string
    Number: int
    Repository: string
    IsDraft: bool
    CheckStatus: CheckStatus option
    ReviewStatus: ReviewStatus option
    HasConflicts: bool
}

type PullRequestGroup = {
    Mine: PullRequest list
    ReviewRequested: PullRequest list
    Involved: PullRequest list
}
```

### Interfaces

```fsharp
type IGitHubClient =
    abstract member GetUsername: unit -> Async<string>
    abstract member FetchPullRequests: username: string -> Async<PullRequestGroup>
```

GitHub access is encapsulated behind `IGitHubClient`. The initial implementation shells out to `gh api graphql` and `gh api user`. This allows swapping to direct HTTP with a personal access token or config-file-based auth later without changing the rest of the app.

Username resolution is part of the same interface, auto-detected via `gh api user --jq .login` in the initial implementation.

## Data Flow

### Startup Sequence

1. `Program.fs` validates `gh` is available by running `gh auth status`. If it fails, exit with a clear error message to stderr.
2. Resolve username via `IGitHubClient.GetUsername`.
3. Create `NotifyIcon` with initial "..." loading state.
4. Start poller — first tick immediate, then every configured interval.

### Poll Cycle

1. `PullRequestPoller` calls `IGitHubClient.FetchPullRequests`.
2. On success: store result as last known good state, pass `PullRequestGroup` to `TrayIcon` for re-rendering.
3. On failure: log error to stderr, keep last known good state, update timestamp to show staleness.

### GitHub Query

Single GraphQL query using the search: `sort:updated-desc type:pr state:open involves:{username}`. The query fetches title, URL, number, repository, draft status, check conclusions, review decisions, and mergeable state.

PRs are classified client-side:
- **Mine**: PR author login matches username
- **Review Requested**: user is in the requested reviewers list
- **Involved**: everything else (commented, mentioned, assigned, etc.)

## Tray Icon & Menu

### Icon Display

The tray icon text shows the total PR count (sum of all three groups). During loading: "...". On the very first load failure (after startup validation passed): keeps "..." until first successful fetch.

### Context Menu Structure

```
My PRs
──────────
  ✅ 👍 repo#123 - PR title
  ❌ ⚔️ repo#456 - [Draft] title
Review Requested
──────────
  ⏳    repo#789 - PR title
Involved
──────────
  n/a
──────────
Last updated: 14:32:05
```

### Status Indicators

| Status | Icons | Condition |
|--------|-------|-----------|
| CI Success | ✅ | All checks passed |
| CI Failure | ❌ | Any check failed |
| CI Pending | ⏳ | Checks still running or no checks |
| Review Approved | 👍 | Approved review |
| Review Changes Requested | 👎 | Changes requested |
| Merge Conflicts | ⚔️ | Mergeable state is conflicting |
| Draft | `[Draft]` prefix | PR is a draft |

Empty sections show "n/a".

### Menu Actions

- Click a PR item: open its URL in the default browser via `Process.Start`
- "Quit" item at the bottom: triggers cancellation and exits

## Configuration

Follows 12-factor: config via environment variables, no config files.

| Variable | Default | Description |
|----------|---------|-------------|
| `GH_TRAY_POLL_INTERVAL` | `60` | Polling interval in seconds |
| `GH_TRAY_LOG_LEVEL` | `Warning` | Minimum log level (Trace/Debug/Information/Warning/Error) |

## Dependencies

### NuGet Packages

- `Microsoft.Extensions.Hosting` — DI container, logging, configuration, host lifetime

### Runtime Dependencies

- `gh` CLI — must be installed and authenticated (`gh auth status` must succeed)
- .NET 10 runtime (or self-contained publish)

### SDK

- .NET 10 SDK
- Target framework: `net10.0-windows`
- `UseWindowsForms: true`

## Application Lifecycle

- The generic host runs alongside the WinForms message loop via `Application.Run` (no visible form)
- Right-click "Quit" menu item or process termination triggers `CancellationToken`
- `NotifyIcon` is disposed on shutdown (removes icon from tray)

## Error Handling

- **Startup**: `gh auth status` fails → exit with error to stderr
- **Polling**: transient failure → log to stderr, keep last known good state, timestamp shows staleness
- **GraphQL errors**: treated as transient polling failures

## Future Considerations (Not In Scope)

- Direct HTTP + personal access token auth (swap `IGitHubClient` implementation)
- Config file for username override
- Azure DevOps support (second implementation of a PR provider interface)
- Custom icons / icon themes
- Notification popups for new PRs
