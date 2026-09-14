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

    /// <summary>Nombre del archivo de base de datos SQLite de notas.</summary>
    public const string DatabaseFileName = "notes.db";

    /// <summary>Prefijo para códigos de compartición de perfiles de sincronización.</summary>
    public const string SyncProfileCodePrefix = "aldune-profile-v2:";

    /// <summary>Prefijos heredados compatibles para códigos de compartición.</summary>
    public static readonly string[] LegacySyncProfileCodePrefixes = ["fanote-profile-v2:", "fanote-profile-v1:"];

    /// <summary>Nombres de variables de entorno para sincronización (actual y heredada).</summary>
    public const string SyncTokenEnvVar = "ALDUNE_SYNC_TOKEN";
    public const string LegacySyncTokenEnvVar = "FANOTE_SYNC_TOKEN";

    public const string SyncTokensEnvVar = "ALDUNE_SYNC_TOKENS";
    public const string LegacySyncTokensEnvVar = "FANOTE_SYNC_TOKENS";

    public const string SyncPortEnvVar = "ALDUNE_SYNC_PORT";
    public const string LegacySyncPortEnvVar = "FANOTE_SYNC_PORT";

    public const string SyncDataDirEnvVar = "ALDUNE_DATA_DIR";
    public const string LegacySyncDataDirEnvVar = "FANOTE_DATA_DIR";

    public const string MonitorIndexEnvVar = "ALDUNE_MONITOR_INDEX";
    public const string LegacyMonitorIndexEnvVar = "FANOTE_MONITOR_INDEX";
}
