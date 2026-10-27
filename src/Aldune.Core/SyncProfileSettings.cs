namespace Aldune.Core;

/// <summary>
/// Una configuración de sincronización independiente. Los campos antiguos de <see cref="AppSettings"/>
/// siguen existiendo para que las versiones anteriores puedan abrir el fichero; <see cref="SyncProfileStore"/>
/// los proyecta siempre sobre el perfil activo.
/// </summary>
public sealed class SyncProfileSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Mis dispositivos";
    public bool SyncEnabled { get; set; }
    public SyncTransportKind SyncTransport { get; set; } = SyncTransportKind.Folder;
    public string? SyncFolderPath { get; set; }
    public string? SyncServerUrl { get; set; }
    public byte[]? WrappedSyncServerToken { get; set; }
    public string? SyncWebDavUsername { get; set; }
    public byte[]? WrappedSyncWebDavPassword { get; set; }
    public bool SyncAutomatically { get; set; }
    public int SyncIntervalMinutes { get; set; } = 15;
    public DateTimeOffset? LastSyncAt { get; set; }
    public string? SyncDeviceId { get; set; }
    public byte[]? WrappedSyncKey { get; set; }
    public byte[]? WrappedPendingSyncKey { get; set; }
    public bool SyncKeyRotationPending { get; set; }
    public SyncScopeKind SyncScope { get; set; } = SyncScopeKind.AllNotes;
    public List<Guid> SyncNoteIds { get; set; } = new();
    public string? SyncTag { get; set; }

    /// <summary>
    /// Con qué clave guarda este perfil sus bases de conflicto en la base de datos (ver <c>SyncBase</c>). El
    /// primer perfil conserva la vacía, que es la de siempre, para no perder lo ya acordado con su almacén; los
    /// demás usan su propio id. Va aparte de <see cref="Id"/> por esa herencia, y no cambia aunque se borre otro perfil.
    /// </summary>
    public string BaseKey { get; set; } = "";
}
