# gh-tray

A Windows system tray application that monitors your GitHub pull requests using the `gh` CLI.

PRs are grouped into three categories:

- **My PRs** — pull requests you authored
- **Review Requested** — pull requests where your review is requested
- **Involved** — pull requests you're otherwise involved in

Each PR shows status indicators for draft, merge conflicts, CI status, and review status using emoji icons.

## Features

- Polls GitHub for open PRs involving you
- Groups and displays PRs in a context menu from the system tray
- Dark/light theme detection with automatic switching
- Global hotkey to open the menu at cursor position (default: `Ctrl+Alt+Shift+G`)
- Optional auto-start with Windows

## Prerequisites

- [GitHub CLI](https://cli.github.com/) (`gh`) installed and authenticated (`gh auth login`)
- .NET 10 SDK (for building from source)

## Install

Download the latest release from [GitHub Releases](https://github.com/jsfr/gh-tray/releases), or build and install locally:

```powershell
.\install.ps1
```

This publishes a self-contained exe to `%LOCALAPPDATA%\gh-tray` and registers it to start with Windows.

To uninstall:

```powershell
.\install.ps1 -Uninstall
```

## Configuration

| Environment Variable | Description | Default |
|---|---|---|
| `GH_TRAY_POLL_INTERVAL` | Polling interval in seconds | `120` |
| `GH_TRAY_LOG_LEVEL` | Log level (`Debug`, `Information`, `Warning`, etc.) | `Information` |
| `GH_TRAY_HOTKEY` | Global hotkey binding (e.g. `Ctrl+Alt+G`) | `Ctrl+Alt+Shift+G` |

## Development

Requires [just](https://github.com/casey/just) as a task runner.

```
just build       # restore and build (Release)
just run         # run the app (Debug)
just fmt         # format with Fantomas
just check       # formatting + lint + analyzers
just publish 1.0 # publish self-contained exe
```
