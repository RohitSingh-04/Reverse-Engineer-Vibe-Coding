; Inno Setup Script for TransparentTaskbar
; Produces a 1-click self-contained installer

#define MyAppName "TransparentTaskbar"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Rohit Singh Personal"
#define MyAppDeveloper "Rohit Singh + Gemini"
#define MyAppURL "https://github.com/yourusername/TransparentTaskbar"
#define MyAppExeName "TransparentTaskbar.exe"

[Setup]
; Unique GUID identifying this application
AppId={{8F1E4491-03E6-49C7-94E5-B9F297E5B5D2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright={#MyAppDeveloper}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
OutputDir=.
OutputBaseFilename=setup
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Launch TransparentTaskbar automatically with Windows (Priority 1 Instant Transparency)"; GroupDescription: "Startup options:"

[Files]
; The self-contained single-file executable published from dotnet publish
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Assets\app.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app.ico"
Name: "{group}\Settings"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--settings"; IconFilename: "{app}\Assets\app.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app.ico"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app.ico"; Tasks: autostart

[Registry]
; Priority 1 Instant Startup: HKCU Run + StartupApproved Enabled + 0s Windows Startup Delay
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize"; ValueType: dword; ValueName: "StartupDelayInMSec"; ValueData: 0; Flags: uninsclearvalue; Tasks: autostart
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"; ValueType: binary; ValueName: "{#MyAppName}"; ValueData: "02 00 00 00 00 00 00 00 00 00 00 00"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
; Option to launch the application immediately upon installation completion
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up config file on complete uninstallation if located in AppData
Type: filesandordirs; Name: "{userappdata}\{#MyAppName}"

[Code]
// Close any running instance of the application before installing or updating
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/f /im {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Restore taskbars to Windows default before killing and deleting files
    Exec(ExpandConstant('{app}\{#MyAppExeName}'), '--restore', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(500);
    Exec('taskkill.exe', '/f /im {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
