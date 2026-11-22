#ifndef PublishDir
  #error Use Build-Tester-Releases.ps1 to supply a fresh audited publish folder.
#endif
; LootDrop Windows x64 per-user installer — Inno Setup 6
; Compile with Build-Tester-Releases.ps1 or open this file in Inno Setup.
#define MyAppName "LootDrop"
#define MyAppVersion "0.7.11"
#define MyAppExeName "LootDrop.exe"

[Setup]
AppId={{ED3AB605-01BD-4EE3-8BDD-5BEE14226B1F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} Version 2 {#MyAppVersion}
AppPublisher=Degenerates Inc.
DefaultDirName={localappdata}\Programs\LootDrop
DefaultGroupName=LootDrop
DisableProgramGroupPage=yes
OutputDir=dist
OutputBaseFilename=LootDrop-Setup
SetupIconFile=Assets\lootdrop-purple.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\LootDrop"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\LootDrop"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch LootDrop"; Flags: nowait postinstall skipifsilent

; User settings and saved game library are intentionally retained after uninstall.






