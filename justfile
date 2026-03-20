project := "src/GhTray"
config := "Release"
rid := "win-x64"

# List available recipes
default:
    @just --list

# Restore dependencies
restore:
    dotnet restore

# Build in Release configuration
build: restore
    dotnet build {{ project }} -c {{ config }} --no-restore

# Run the app (Debug configuration)
run:
    dotnet run --project {{ project }}

# Format F# source files
fmt:
    dotnet fantomas src/

# Check formatting, lint, and run analyzers
check: check-fmt lint analyze

# Check formatting without modifying files
check-fmt:
    dotnet fantomas --check src/

# Lint F# source files
lint:
    dotnet dotnet-fsharplint lint GhTray.slnx

# Run F# analyzers
analyze:
    dotnet msbuild "{{ project }}/GhTray.fsproj" "-t:AnalyzeFSharpProject"

# Publish self-contained single-file exe
publish version="0.0.0-dev":
    dotnet publish {{ project }} -c {{ config }} --self-contained -r {{ rid }} -p:PublishSingleFile=true -p:InformationalVersion={{ version }}

# Generate changelog
changelog:
    git cliff -o CHANGELOG.md

# Build Windows installer with InnoSetup
build-installer version="0.0.0-dev":
    iscc /DAppVersion={{ version }} installer/gh-tray.iss

# Publish to a specific output directory
publish-to output version="0.0.0-dev":
    dotnet publish {{ project }} -c {{ config }} --self-contained -r {{ rid }} -p:PublishSingleFile=true -p:InformationalVersion={{ version }} -o {{ output }}
