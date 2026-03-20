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
- Multi-account support — select which `gh` CLI account to use
- JSON config file with optional environment variable overrides
- File logging for troubleshooting when not running from a terminal
- Dark/light theme detection with automatic switching
- Global hotkey to open the menu at cursor position (default: `Ctrl+Alt+Shift+G`)
- Optional auto-start with Windows

## Prerequisites

- [GitHub CLI](https://cli.github.com/) (`gh`) installed and authenticated (`gh auth login`)
- .NET 10 SDK (for building from source)

## Install

### winget

```
winget install jsfr.gh-tray
```

### Installer

Download the latest `gh-tray-*-win-x64-setup.exe` from [GitHub Releases](https://github.com/jsfr/gh-tray/releases) and run it. The installer supports silent install with `/VERYSILENT /SUPPRESSMSGBOXES`.

### Build from source

```powershell
.\install.ps1
```

This publishes a self-contained exe to `%LOCALAPPDATA%\gh-tray` and registers it to start with Windows.

To uninstall:

```powershell
.\install.ps1 -Uninstall
```

## Configuration

Configuration is read from `%APPDATA%\gh-tray\config.json`. All fields are optional — missing fields use defaults.

```json
{
  "account": "my-github-account",
  "pollInterval": 120,
  "logLevel": "Information",
  "hotkey": "Ctrl+Alt+Shift+G",
  "logFile": "C:\\Users\\me\\AppData\\Roaming\\gh-tray\\gh-tray.log"
}
```

| Field | Type | Default | Description |
|---|---|---|---|
| `account` | string | (system default) | `gh` CLI account name (as shown in `gh auth status`) |
| `pollInterval` | int | `120` | Polling interval in seconds |
| `logLevel` | string | `Information` | Log level: `Debug`, `Information`, `Warning`, `Error` |
| `hotkey` | string | `Ctrl+Alt+Shift+G` | Global hotkey binding |
| `logFile` | string | (none) | Path to a log file; when set, logs are written to this file |

### Environment variable overrides

Environment variables override config file values when set:

| Environment Variable | Overrides |
|---|---|
| `GH_TRAY_POLL_INTERVAL` | `pollInterval` |
| `GH_TRAY_LOG_LEVEL` | `logLevel` |
| `GH_TRAY_HOTKEY` | `hotkey` |

## Development

Requires [just](https://github.com/casey/just) as a task runner.

```
just build       # restore and build (Release)
just run         # run the app (Debug)
just fmt         # format with Fantomas
just check       # formatting + lint + analyzers
just publish 1.0 # publish self-contained exe
```
