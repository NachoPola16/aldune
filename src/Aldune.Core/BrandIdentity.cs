namespace Aldune.Core;

/// <summary>
/// Constantes centrales de identidad de marca para Aldune.
/// Centralizar estos valores aquí permite que cambios futuros de nombre de aplicación,
/// rutas de configuración o variables de entorno puedan gestionarse desde un único lugar.
/// </summary>
public static class BrandIdentity
{
    /// <summary>Nombre oficial y visible de la aplicación.</summary>
    public const string AppName = "Aldune";

    /// <summary>Nombre de la carpeta de datos en %LOCALAPPDATA%.</summary>
    public const string AppDataDirectoryName = "Aldune";

    /// <summary>Nombre de la carpeta de datos heredada para migración automática.</summary>
    public const string LegacyAppDataDirectoryName = "Fanote";

    /// <summary>Nombre del ejecutable principal publicado.</summary>
    public const string ExecutableName = "aldune.exe";

    /// <summary>Nombre del valor en HKCU\...\Run para el arranque automático.</summary>
    public const string StartupRegistryKey = "Aldune";

    /// <summary>Nombre del valor de registro heredado para migración en el arranque.</summary>
    public const string LegacyStartupRegistryKey = "Fanote";

    /// <summary>Clase de mensaje Win32 para registro de hotkeys globales.</summary>
    public const string WindowMessageClassName = "AlduneHotkey";

    /// <summary>
    /// Mutex de instancia única. El instalador (<c>installer/Aldune.iss</c>) lo usa para saber si
    /// Aldune está abierta al actualizar o desinstalar: si cambia aquí, hay que cambiarlo allí.
    /// </summary>
    public const string SingleInstanceMutexName = "AlduneSingleInstance";

    /// <summary>Evento con el que una segunda instancia avisa a la primera antes de cerrarse.</summary>
    public const string SingleInstanceActivateEventName = "AlduneActivate";

    /// <summary>
    /// Evento con el que el instalador pide a Aldune que se cierre (guardando las notas abiertas) para
    /// poder sustituir el ejecutable sin que haya que cerrarla a mano. Mismo nombre en
    /// <c>installer/Aldune.iss</c>.
    /// </summary>
    public const string SingleInstanceQuitEventName = "AlduneQuit";

    /// <summary>Nombre del archivo de base de datos SQLite de notas.</summary>
    public const string DatabaseFileName = "notes.db";

    /// <summary>Prefijo para códigos de compartición de perfiles de sincronización.</summary>
    public const string SyncProfileCodePrefix = "aldune-profile-v2:";

    /// <summary>
    /// Prefijos heredados que se siguen aceptando al importar un código de compartición. Los
    /// códigos ya emitidos con un nombre anterior (Fanote) tienen que seguir funcionando, así que
    /// al rebautizar la aplicación se añade el prefijo vigente a esta lista, no se sustituye.
    /// </summary>
    public static readonly string[] LegacySyncProfileCodePrefixes = ["fanote-profile-v2:", "fanote-profile-v1:"];

    /// <summary>
    /// Nombres de las variables de entorno de sincronización, que son los mismos que esperan
    /// docker-compose.sync*.yml y .env.example: si se renombra alguna hay que cambiarla también ahí.
    /// Las leen el cliente (SyncService), la aplicación (App) y el servidor (Aldune.SyncServer).
    /// </summary>
    public const string SyncTokenEnvVar = "ALDUNE_SYNC_TOKEN";

    public const string SyncTokensEnvVar = "ALDUNE_SYNC_TOKENS";

    public const string SyncPortEnvVar = "ALDUNE_SYNC_PORT";

    public const string SyncDataDirEnvVar = "ALDUNE_DATA_DIR";

    /// <summary>Monitor al que forzar el dock en pruebas manuales (ver App.OnDisplaySettingsChanged).</summary>
    public const string MonitorIndexEnvVar = "ALDUNE_MONITOR_INDEX";
}
