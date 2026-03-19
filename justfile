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

# Check formatting without modifying files
check:
    dotnet fantomas --check src/

# Publish self-contained single-file exe
publish version="0.0.0-dev":
    dotnet publish {{ project }} -c {{ config }} --self-contained -r {{ rid }} -p:PublishSingleFile=true -p:InformationalVersion={{ version }}

# Publish to a specific output directory
publish-to output version="0.0.0-dev":
    dotnet publish {{ project }} -c {{ config }} --self-contained -r {{ rid }} -p:PublishSingleFile=true -p:InformationalVersion={{ version }} -o {{ output }}
