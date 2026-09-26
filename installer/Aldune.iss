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
; Aldune se cierra sola al empezar (ver [Code]): no hace falta cerrarla a mano y el asistente no queda
; debajo de las notas y el dock, que están siempre encima. Sin AppMutex, que paraba el instalador hasta
; cerrarla a mano. El Restart Manager queda de respaldo para versiones que no conocen el aviso de
; cierre (1.0.3 y anteriores): en "Preparando la instalación" ofrece cerrarla.
CloseApplications=force
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
; En una instalación silenciosa no hay casilla: se vuelve a abrir solo si estaba abierta.
Filename: "{app}\{#MyAppExeName}"; Flags: nowait skipifnotsilent; Check: WasRunning

[Registry]
; El arranque con Windows lo escribe la propia app (StartupRegistration), no el instalador. Sin esto
; la entrada se quedaba al desinstalar, apuntando a un ejecutable que ya no existe. Las notas en
; %LOCALAPPDATA%\Aldune no se tocan: desinstalar no debe borrar datos del usuario.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "Aldune"; ValueType: none; Flags: uninsdeletevalue dontcreatekey

[CustomMessages]
english.CloseAldune=Aldune is still running. Close it (tray icon > Exit) and press Retry.
spanish.CloseAldune=Aldune sigue abierta. Ciérrala (icono de la bandeja > Salir) y pulsa Reintentar.

[Code]
// Mismos nombres que BrandIdentity.SingleInstanceMutexName y SingleInstanceQuitEventName.
const
  AppMutexName = 'AlduneSingleInstance';
  QuitEventName = 'AlduneQuit';
  EVENT_MODIFY_STATE = $0002;

var
  ClosedRunningApp: Boolean;
  Installed: Boolean;
  ExeToRelaunch: String;

function OpenEvent(DesiredAccess: Cardinal; InheritHandle: BOOL; Name: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(Handle: THandle): BOOL;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): BOOL;
  external 'CloseHandle@kernel32.dll stdcall';

// Pide a Aldune que se cierre (guarda las notas abiertas y sale) y espera hasta 10 s. Devuelve si
// ya no está abierta. Una versión que no conoce el aviso no crea el evento: se deja al Restart
// Manager (instalar) o a la pregunta de CloseOrAsk (desinstalar).
function CloseRunningAldune(): Boolean;
var
  Handle: THandle;
  Waited: Integer;
begin
  Result := not CheckForMutexes(AppMutexName);
  if Result then Exit;
  Handle := OpenEvent(EVENT_MODIFY_STATE, False, QuitEventName);
  if Handle = 0 then Exit;
  SetEvent(Handle);
  CloseHandle(Handle);
  Waited := 0;
  while CheckForMutexes(AppMutexName) and (Waited < 10000) do
  begin
    Sleep(200);
    Waited := Waited + 200;
  end;
  Result := not CheckForMutexes(AppMutexName);
  ClosedRunningApp := Result;
end;

function WasRunning(): Boolean;
begin
  Result := ClosedRunningApp;
end;

// Antes de enseñar el asistente: así ninguna ventana de Aldune (siempre encima) lo tapa.
function InitializeSetup(): Boolean;
begin
  CloseRunningAldune();
  Result := True;
end;

procedure InitializeWizard();
begin
  ExeToRelaunch := AddBackslash(WizardDirValue) + '{#MyAppExeName}';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then Installed := True;
end;

// Si se cerró Aldune para instalar y al final no se instaló (cancelar), se vuelve a abrir la de antes.
procedure DeinitializeSetup();
var
  ResultCode: Integer;
begin
  if ClosedRunningApp and not Installed and (ExeToRelaunch <> '') and FileExists(ExeToRelaunch) then
    Exec(ExeToRelaunch, '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
end;

// Desinstalar: se cierra sola si puede; si es una versión antigua, se pide cerrarla a mano.
function InitializeUninstall(): Boolean;
begin
  Result := True;
  while not CloseRunningAldune() do
  begin
    if MsgBox(CustomMessage('CloseAldune'), mbError, MB_RETRYCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;
end;
