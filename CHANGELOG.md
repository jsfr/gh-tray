# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- add file logging support via logFile config option

### Fixed

- allow partial config files by skipping missing option fields

## [0.0.4] - 2026-03-20

### Added

- add config file support and GitHub account selection

## [0.0.3] - 2026-03-20

### Added

- add Windows installer and winget support

### CI

- split CI/CD into separate CI and release workflows

### Fixed

- quote ISCC define flag for PowerShell compatibility
- use cmd shell for ISCC to avoid PowerShell path issues
- call ISCC directly in CI instead of through just

## [0.0.2] - 2026-03-19

### CI

- auto-update CHANGELOG.md on release

### Fixed

- resolve all lint, analyzer, and nullability warnings
- disable FL0014 lint rule conflicting with compiler FS0760
- add restore step before check in CI

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


