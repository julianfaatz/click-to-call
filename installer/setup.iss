; Inno Setup script for Click to Call.
; Built by build.ps1:  ISCC.exe /DAppVersion=1.2.3 /DSourceDir=..\artifacts\publish installer\setup.iss

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish"
#endif

#define AppName "Click to Call"
#define AppExe "ClickToCall.exe"
#define AppUrl "https://github.com/julianfaatz/click-to-call"

[Setup]
AppId={{6F1C7A52-3E2B-4C1D-9A5E-8B7D2F4C1A93}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Julian Faatz
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}

; Per-user installation – no admin rights required
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto

OutputDir=..\artifacts
OutputBaseFilename=ClickToCall-{#AppVersion}-setup
SetupIconFile=..\src\ClickToCall\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
LicenseFile=..\LICENSE

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
german.AutostartTask=Mit Windows starten
english.AutostartTask=Start with Windows
german.LaunchApp={#AppName} jetzt starten
english.LaunchApp=Launch {#AppName} now

[Tasks]
Name: "autostart"; Description: "{cm:AutostartTask}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{#AppName} – Einstellungen"; Filename: "{app}\{#AppExe}"; Parameters: "--settings"; Languages: german
Name: "{group}\{#AppName} – Settings"; Filename: "{app}\{#AppExe}"; Parameters: "--settings"; Languages: english
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ClickToCall"; \
    ValueData: """{app}\{#AppExe}"" --autostart"; Tasks: autostart; Flags: uninsdeletevalue; Check: not WizardSilent

[Run]
; Register tel:/callto:/sip: handlers
Filename: "{app}\{#AppExe}"; Parameters: "--register"; Flags: runhidden waituntilterminated
; Interactive install: offer to launch
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent
; Silent install (in-app update): restart the app automatically
Filename: "{app}\{#AppExe}"; Parameters: "--autostart"; Flags: nowait; Check: WizardSilent

[UninstallRun]
Filename: "{app}\{#AppExe}"; Parameters: "--unregister"; Flags: runhidden waituntilterminated; RunOnceId: "Unregister"

[Code]
procedure StopRunningApp();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopRunningApp();
  Result := True;
end;
