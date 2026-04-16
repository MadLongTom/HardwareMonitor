#define MyAppName "HardwareMonitor"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef DistDir
  #define DistDir "dist"
#endif
#ifndef OutputName
  #define OutputName "HardwareMonitor-setup"
#endif
#ifndef ArchAllowed
  #define ArchAllowed "x64compatible"
#endif
#define MyAppExeName "HardwareMonitor.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=.
OutputBaseFilename={#OutputName}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed={#ArchAllowed}
ArchitecturesInstallIn64BitMode={#ArchAllowed}
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardStyle=modern

[Files]
Source: "{#DistDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
