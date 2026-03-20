# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Version Control

This repository uses **Jujutsu (jj)**, not git. Always use `jj` commands for version control operations.

## Build & Development Commands

All commands use `just` as the task runner:

- `just build` — restore and build (Release)
- `just run` — run the app (Debug)
- `just fmt` — format F# files with Fantomas
- `just check` — run all checks: formatting, lint, analyzers
- `just check-fmt` — check formatting only
- `just lint` — run FSharpLint
- `just analyze` — run G-Research F# analyzers
- `just publish <version>` — publish self-contained single-file exe
- `just changelog` — generate changelog with git-cliff

Recipe names use `-` as separator (e.g. `check-fmt`, `publish-to`).

## Architecture

**gh-tray** is a Windows system tray application that monitors GitHub pull requests. It's a single F# project targeting `net10.0-windows` using WinForms (`NotifyIcon` + `ContextMenuStrip`).

### Hosting Model

Uses `Microsoft.Extensions.Hosting` with a `IHostedService` (`PullRequestPoller`) for background polling. The host runs alongside a WinForms message loop in `Program.fs`.

### Source Files (compilation order matters in F#)

- **Types.fs** — Domain types: `CheckStatus`, `ReviewStatus`, `PullRequest`, `PullRequestGroup`
- **Config.fs** — `AppConfig` record and JSON config file loader (`%APPDATA%\gh-tray\config.json`); uses `FSharp.SystemTextJson` with `WithSkippableOptionFields` so partial configs work
- **FileLogger.fs** — Minimal `ILoggerProvider` that writes timestamped log lines to a file
- **GitHubClient.fs** — `IGitHubClient` interface and `GhCliClient` implementation wrapping `gh` CLI with GraphQL queries; accepts optional token for multi-account support
- **EmojiRenderer.fs** — Renders emoji to bitmaps using Direct2D/DirectWrite (Vortice.Direct2D1)
- **AutoStart.fs** — Windows registry integration for auto-start on login
- **HotKey.fs** — Global hotkey registration via Win32 P/Invoke (`RegisterHotKey`/`UnregisterHotKey`)
- **TrayIcon.fs** — `TrayApp` class: builds the context menu, renders PR sections with status icons, detects dark/light theme
- **PullRequestPoller.fs** — `IHostedService` that polls GitHub on a timer and updates the tray icon
- **Program.fs** — Entry point: loads config, resolves account token, validates `gh auth`, configures DI, starts host + message loop

### Key Design Decisions

- GitHub data comes from shelling out to `gh` CLI with GraphQL, not the GitHub API directly — avoids token management
- Multi-account support: when `account` is set in config, the app runs `gh auth token --user <account>` at startup and passes the token via `GH_TOKEN` in `ProcessStartInfo.Environment` (process-scoped, no side effects)
- PRs are grouped into: Mine, Review Requested, Involved
- Theme detection reads `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize` for dark/light mode
- Emoji rendering uses Direct2D because WinForms `Graphics` doesn't support color emoji

### Configuration

Configuration is read from `%APPDATA%\gh-tray\config.json`. All fields are optional. Environment variables override config file values:

- `GH_TRAY_POLL_INTERVAL` — overrides `pollInterval` (default: 120 seconds)
- `GH_TRAY_LOG_LEVEL` — overrides `logLevel` (default: Information)
- `GH_TRAY_HOTKEY` — overrides `hotkey` (default: Ctrl+Alt+Shift+G)

## Project Configuration

- Central Package Management (`Directory.Packages.props`)
- `TreatWarningsAsErrors` is enabled — including nullability (FS3261), handle nullable C# interop with `Option.ofObj`
- Lock file for NuGet restore (`RestorePackagesWithLockFile`)
- Pre-commit hook runs `just check-fmt` via prek; pre-push runs `just check`

## Commit Style

Uses [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `refactor:`, `ci:`, `chore:`, `docs:`).
