param(
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

$AppName = "GhTray"
$InstallDir = "$env:LOCALAPPDATA\gh-tray"
$RegistryPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
$RegistryKey = "GhTray"

function Stop-GhTray {
    $proc = Get-Process -Name $AppName -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Host "Stopping $AppName..."
        $proc | Stop-Process -Force
        Start-Sleep -Seconds 2
    }
}

if ($Uninstall) {
    Write-Host "Uninstalling $AppName..."

    Stop-GhTray

    if (Get-ItemProperty -Path $RegistryPath -Name $RegistryKey -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $RegistryPath -Name $RegistryKey
        Write-Host "Removed auto-start registry entry."
    }

    if (Test-Path $InstallDir) {
        Remove-Item -Path $InstallDir -Recurse -Force
        Write-Host "Removed install directory: $InstallDir"
    }

    Write-Host "$AppName uninstalled."
}
else {
    Write-Host "Installing $AppName..."

    Stop-GhTray

    Write-Host "Publishing to $InstallDir..."
    dotnet publish src/GhTray -c Release --self-contained -r win-x64 -p:PublishSingleFile=true -o $InstallDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed."
        exit 1
    }

    $exePath = Join-Path $InstallDir "$AppName.exe"
    New-ItemProperty -Path $RegistryPath -Name $RegistryKey -Value $exePath -PropertyType String -Force | Out-Null
    Write-Host "Registered auto-start: $exePath"

    Write-Host "$AppName installed to $InstallDir."
}
