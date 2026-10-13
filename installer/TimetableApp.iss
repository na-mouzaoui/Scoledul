; Script d'installation Inno Setup (https://jrsoftware.org/isinfo.php) — licence Inno Setup (libre).
; Compiler après avoir publié l'application :
;   dotnet publish src/Timetable.Desktop/Timetable.Desktop.csproj -c Release -r win-x64 ^
;     --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish
; Puis ouvrir ce fichier dans Inno Setup Compiler et cliquer sur « Compile ».

#define MyAppName "Générateur d'emplois du temps"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Open Timetable"
#define MyAppExeName "TimetableApp.exe"

[Setup]
AppId={{8F2A6E9C-7C1B-4A3E-9F4D-1E5B2C3A7D61}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\TimetableApp
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\publish\installer
OutputBaseFilename=TimetableApp-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le bureau"; GroupDescription: "Raccourcis :"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Lancer {#MyAppName}"; Flags: nowait postinstall skipifsilent
