; BSManager installer (Inno Setup 6)
;
; CI builds it with:  iscc /DAppVersion=<version> /DSourceExe=<path to BSManager.exe> /O<output dir> installer\BSManager.iss
; Installs per user by default (no admin prompt), so the built-in auto-updater can replace BSManager.exe in place;
; "Install for all users" (Program Files) stays available from the privileges dialog.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceExe
  #define SourceExe "..\out\BSManager.exe"
#endif

#define AppName "BSManager"
#define AppExe "BSManager.exe"
#define AppUrl "https://github.com/mann1x/BSManager"
; Same name as the single-instance mutex in Program.cs: Setup waits for (or asks to close) a running BSManager
#define AppMutexName "{{67489549-940B-48FF-9B6E-70D31B4C6E71}"

[Setup]
AppId={{CFD565F9-655D-49B8-A523-7809107BC57F}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=ManniX
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
AppMutex={#AppMutexName}
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
LicenseFile=..\LICENSE
SetupIconFile=..\bsmanager.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputBaseFilename=BSManager-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startup"; Description: "Run BSManager at Windows logon"; GroupDescription: "Startup:"

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "{#AppExe}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
; The same value BSManager's own "Run at Startup" menu item manages
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BSManager"; ValueData: "{app}\{#AppExe}"; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Elevated task registered by the "OpenXR to SteamVR" option (may need administrator rights to delete; ignored if it fails)
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""BSManager\OpenXR runtime"" /F"; Flags: runhidden; RunOnceId: "DelOpenXRTask"

[UninstallDelete]
Type: files; Name: "{app}\BSManager.log"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // Run at Startup may also have been enabled from the tray menu after installing
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'BSManager');
end;
