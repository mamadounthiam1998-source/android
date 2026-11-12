; ======================================================================
;  GHOST ANDROID — Script d'installation Inno Setup
;  Produit un installateur Windows professionnel (Win 10 / Win 11).
;  Compilation (sur Windows, après `dotnet publish`) :
;    ISCC.exe /DAppVersion=0.1.0 /DSourceDir=..\artifacts\app installer\GhostAndroid.iss
;  Le workflow GitHub Actions (windows-latest) exécute cette étape automatiquement.
; ======================================================================

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\app"
#endif

#define AppName "GHOST ANDROID"
#define AppPublisher "GHOST ANDROID"
#define AppExe "GhostAndroid.exe"

[Setup]
AppId={{E3B8F2A1-9C4D-4F7E-9A11-0A84FF00E5FF}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=GhostAndroid-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\src\GhostAndroid.App\Assets\ghost.ico
PrivilegesRequired=admin

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; GroupDescription: "Raccourcis :"

[Files]
; Contenu publié de l'application WPF.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Android Platform Tools (adb/fastboot) — copiés s'ils ont été placés à côté du build.
Source: "{#SourceDir}\tools\platform-tools\*"; DestDir: "{app}\tools\platform-tools"; \
    Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Désinstaller {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Lancer {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Nettoie les fichiers générés localement par l'application (base, journaux).
Type: filesandordirs; Name: "{userappdata}\GhostAndroid"
