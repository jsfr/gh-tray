#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif

#define AppName "gh-tray"
#define AppPublisher "jsfr"
#define AppExeName "GhTray.exe"
#define AppUrl "https://github.com/jsfr/gh-tray"

[Setup]
AppId={{B8F4E6A2-3C1D-4E5F-9A7B-2D8C6F0E1A3B}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=gh-tray-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; Description: "Start {#AppName} automatically when you log in"; GroupDescription: "Additional options:"

[Files]
Source: "..\src\GhTray\bin\Release\net10.0-windows\win-x64\publish\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "GhTray"; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
