# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.0.2] - 2026-03-19

### Fixed

- resolve all lint, analyzer, and nullability warnings

## [0.0.1] - 2026-03-19

### Added

- add GhTray F# project with WinForms and Hosting
- add domain types for PR status and GitHub client
- add GitHub CLI wrapper with GraphQL query and PR classification
- add tray icon with context menu, PR sections, and status icons
- add pull request poller as hosted service
- add entry point with gh auth validation and host setup
- add theme-aware tray icon with dark/light mode detection
- add local install, auto-start toggle, and CI/CD pipeline
- add global hotkey to open context menu at cursor position

### CI

- use git-cliff for release notes generation

### Fixed

- update context menu colors on theme change
- preserve prefix/title tag for draft PR alignment in renderer


