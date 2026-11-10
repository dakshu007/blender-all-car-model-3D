; Inno Setup script - Windows installer (BlossomRally-Setup-<version>.exe) for the tester build.
; Used by .github/workflows/build-game.yml:  iscc /DAppVersion=0.9.0 /DSourceDir=<folder with BlossomRally.exe> BlossomRally.iss
#ifndef AppVersion
  #define AppVersion "0.9.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\UnityProject\Builds\ci\win"
#endif

[Setup]
AppId={{6F2A9C1E-4B7D-4E8A-9C3B-B10550A0A0A1}
AppName=Blossom Rally
AppVersion={#AppVersion}
AppVerName=Blossom Rally {#AppVersion} (test build)
AppPublisher=Blossom Rally
DefaultDirName={autopf}\Blossom Rally
DefaultGroupName=Blossom Rally
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=BlossomRally-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
SetupIconFile=BlossomRally.ico
UninstallDisplayIcon={app}\BlossomRally.exe

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion; Excludes: "*_BurstDebugInformation_DoNotShip\*"

[Icons]
Name: "{group}\Blossom Rally"; Filename: "{app}\BlossomRally.exe"
Name: "{group}\Uninstall Blossom Rally"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Blossom Rally"; Filename: "{app}\BlossomRally.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\BlossomRally.exe"; Description: "Play Blossom Rally now"; Flags: nowait postinstall skipifsilent
