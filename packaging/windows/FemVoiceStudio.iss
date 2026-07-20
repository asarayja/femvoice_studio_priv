; Inno Setup script for FemVoice Studio (Windows installer).
;
; Produces a classic "Setup.exe" install wizard that installs the self-contained
; Avalonia desktop app to Program Files, creates Start-menu (+ optional desktop)
; shortcuts, and registers an uninstaller in "Apps & features".
;
; Payload = the single self-contained, code-signed exe produced by:
;   dotnet publish FemVoice.Avalonia/FemVoice.Avalonia.csproj -c Release -r win-x64 `
;     --self-contained true -p:PublishSingleFile=true `
;     -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o dist/win-x64
;
; Build the installer:   ISCC.exe packaging\windows\FemVoiceStudio.iss
; Output:                dist\FemVoice-Studio-Setup.exe   (sign it afterwards)

#define AppName "FemVoice Studio"
#define AppVersion "0.1.0"
#define AppPublisher "Asarayja"
#define AppExeName "FemVoice.Studio.exe"
#define RepoRoot SourcePath + "..\..\"

[Setup]
; Stable AppId — keep this GUID constant across versions so upgrades replace in place.
AppId={{B7E4D3A1-2C56-4F89-A1B0-FE33C0DE1234}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
OutputDir={#RepoRoot}dist
OutputBaseFilename=FemVoice-Studio-Setup
SetupIconFile={#RepoRoot}logo.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin

[Languages]
; English first = default language (used when the OS locale has no better match).
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "norwegian"; MessagesFile: "compiler:Languages\Norwegian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#RepoRoot}dist\win-x64\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Avinstaller {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
