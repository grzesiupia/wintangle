; Wintangle — Inno Setup installer script
;
; Build from the repository root after publishing:
;   dotnet publish src/Wintangle.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o artifacts/publish
;
; CI (the Release workflow) always passes /DAppVersion, derived from the
; GitVersion tag (v1.0.8 -> 1.0.8) — never hard-code a version here.
;
; The publish output directory can be overridden:
;   iscc build\setup.iss /DAppVersion=1.0.8 /DSourceDir=C:\path\to\publish
;
; Relative paths are resolved against this script's directory (build\).

#ifndef AppVersion
  ; Fallback only — the Release workflow always passes /DAppVersion (tag-derived).
  #define AppVersion "0.0.0"
#endif

#ifndef SourceDir
  ; Default matches the Release workflow's publish output (repo\artifacts\publish).
  #define SourceDir "..\artifacts\publish"
#endif

[Setup]
AppId={{3F8E4A72-9C1D-4B6E-A5F0-2D8C7E6B4A91}}
AppName=Wintangle
AppVersion={#AppVersion}
AppVerName=Wintangle {#AppVersion}
AppPublisher=Wintangle contributors
AppPublisherURL=https://github.com/wintangle/wintangle
AppSupportURL=https://github.com/wintangle/wintangle/issues
AppUpdatesURL=https://github.com/wintangle/wintangle/releases
AppCopyright=Copyright (C) 2024-2026 Wintangle contributors
LicenseFile=..\LICENSE
DefaultDirName={autopf}\Wintangle
DefaultGroupName=Wintangle
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=Output
OutputBaseFilename=wintangle-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\Wintangle.App\Assets\wintangle.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName=Wintangle
UninstallDisplayIcon={app}\Wintangle.App.exe
AppMutex=Local\Wintangle.SingleInstance
CloseApplications=yes
RestartApplications=no

[Files]
; License files
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\fonts\OFL.txt"; DestDir: "{app}\licenses"; DestName: "JetBrainsMono-OFL.txt"; Flags: ignoreversion
; Everything from the publish output: the single-file Wintangle.App.exe plus
; the WPF native runtime DLLs (D3DCompiler_47_cor3.dll, PresentationNative_cor3.dll, ...).
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; JetBrains Mono fonts bundled for clean typography on systems without it pre-installed.
Source: "..\assets\fonts\*.ttf"; DestDir: "{autofonts}"; FontInstall: "JetBrains Mono"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
; No IconFilename set: Inno Setup auto-picks the first icon embedded in the
; .exe (Assets/wintangle.ico via ApplicationIcon in the csproj — verified
; assumption; keep the Icons section in sync if that changes).
Name: "{autoprograms}\Wintangle"; Filename: "{app}\Wintangle.App.exe"

[Registry]
; Clean up the autostart Run key entry if the user enabled it during application usage.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "wintangle"; Flags: dontcreatekey uninsdeletevalue

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Run]
Filename: "{app}\Wintangle.App.exe"; Description: "Launch Wintangle"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  // Terminate running Wintangle process before uninstalling files
  Exec('taskkill.exe', '/F /IM Wintangle.App.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
end;
