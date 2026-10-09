#ifndef AppVersion
  #define AppVersion "0.4.0"
#endif
[Setup]
AppId={{D4E2F5D2-191C-4E90-92A8-32AE956A0581}
AppName=X / WHIP
AppVersion={#AppVersion}
AppPublisher=roach01
AppPublisherURL=https://github.com/roach01/x-whip
AppSupportURL=https://github.com/roach01/x-whip/issues
AppUpdatesURL=https://github.com/roach01/x-whip/releases
DefaultDirName={localappdata}\Programs\XWhip
DefaultGroupName=X WHIP
PrivilegesRequired=lowest
MinVersion=10.0
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
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
[Files]
Source: "..\build\XWhip.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\PHOTO-CREDITS.md"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{group}\X WHIP"; Filename: "{app}\XWhip.exe"
Name: "{autodesktop}\X WHIP"; Filename: "{app}\XWhip.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\XWhip.exe"; Description: "Let him clock in"; Flags: nowait postinstall skipifsilent
