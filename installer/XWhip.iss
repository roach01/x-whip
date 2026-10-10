#ifndef AppVersion
  #define AppVersion "0.6.0"
#endif
[Setup]
AppId={{D4E2F5D2-191C-4E90-92A8-32AE956A0581}
AppName=X了么
AppVersion={#AppVersion}
AppPublisher=roach01
AppPublisherURL=https://github.com/roach01/x-whip
AppSupportURL=https://github.com/roach01/x-whip/issues
AppUpdatesURL=https://github.com/roach01/x-whip/releases
DefaultDirName={localappdata}\Programs\XWhip
DefaultGroupName=X了么
PrivilegesRequired=lowest
MinVersion=10.0
DisableWelcomePage=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
OutputBaseFilename=XWhipSetup-{#AppVersion}
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\XWhip.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
[Files]
Source: "..\build\XWhip.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\PHOTO-CREDITS.md"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{group}\X了么"; Filename: "{app}\XWhip.exe"
Name: "{autodesktop}\X了么"; Filename: "{app}\XWhip.exe"
[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
    Exec(ExpandConstant('{app}\XWhip.exe'), '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
end;
