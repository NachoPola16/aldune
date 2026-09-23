#define MyAppName "Aldune"
#define MyAppPublisher "Aldune"
#define MyAppExeName "aldune.exe"
#define SourceDir "..\publish\portable"
#define MyAppVersion GetVersionNumbersString(SourceDir + "\" + MyAppExeName)

[Setup]
AppId={{B7E4E3D1-4A6B-4F30-9A5E-7A9E2B2D0A51}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\Aldune
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
; Mismo nombre que BrandIdentity.SingleInstanceMutexName: pide cerrar Aldune antes de instalar o
; desinstalar, en vez de fallar al sustituir o borrar el ejecutable en uso.
AppMutex=AlduneSingleInstance
RestartApplications=no
SetupIconFile=..\src\Aldune\Assets\aldune.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\dist
OutputBaseFilename=Aldune-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion restartreplace

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Registry]
; El arranque con Windows lo escribe la propia app (StartupRegistration), no el instalador. Sin esto
; la entrada se quedaba al desinstalar, apuntando a un ejecutable que ya no existe. Las notas en
; %LOCALAPPDATA%\Aldune no se tocan: desinstalar no debe borrar datos del usuario.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "Aldune"; ValueType: none; Flags: uninsdeletevalue dontcreatekey
