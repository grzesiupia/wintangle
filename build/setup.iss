; wintangle — Inno Setup installer script
;
; Build from the repository root after publishing:
;   dotnet publish src/Wintangle.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o artifacts/publish
;   iscc build\setup.iss /DAppVersion=1.0.0
;
; The publish output directory can be overridden:
;   iscc build\setup.iss /DAppVersion=1.0.0 /DSourceDir=C:\path\to\publish
;
; Relative paths are resolved against this script's directory (build\).

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#ifndef SourceDir
  ; Default matches the Release workflow's publish output (repo\artifacts\publish).
  #define SourceDir "..\artifacts\publish"
#endif

[Setup]
AppId={{3F8E4A72-9C1D-4B6E-A5F0-2D8C7E6B4A91}}
AppName=wintangle
AppVersion={#AppVersion}
AppVerName=wintangle {#AppVersion}
AppPublisher=wintangle contributors
AppPublisherURL=https://github.com/wintangle/wintangle
DefaultDirName={userpf}\wintangle
DefaultGroupName=wintangle
DisableProgramGroupPage=yes
; Per-user install: no UAC prompt, and the HKCU Run key autostart stays
; consistent with a user-level installation.
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=wintangle-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Release icon: the same Assets/wintangle.ico that is embedded in the exe.
SetupIconFile=..\src\Wintangle.App\Assets\wintangle.ico
; The app is published as win-x64 self-contained; restrict to 64-bit (incl. ARM64 emulation).
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\Wintangle.App.exe
CloseApplications=yes

[Files]
; Everything from the publish output: the single-file Wintangle.App.exe plus
; the WPF native runtime DLLs (D3DCompiler_47_cor3.dll, PresentationNative_cor3.dll, ...).
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; No IconFilename set: Inno Setup auto-picks the first icon embedded in the
; .exe (Assets/wintangle.ico via ApplicationIcon in the csproj — verified
; assumption; keep the Icons section in sync if that changes).
Name: "{autoprograms}\wintangle"; Filename: "{app}\Wintangle.App.exe"

[Run]
Filename: "{app}\Wintangle.App.exe"; Description: "Launch wintangle"; Flags: nowait postinstall skipifsilent
