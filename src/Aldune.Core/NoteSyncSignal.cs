namespace Aldune.Core;

public enum SyncSignalState
{
    Hidden,
    Synced,
    Pending,
    Conflict,
    Excluded,
}

/// <summary>
/// La señal ▂▄▆█ al pie de cada nota: estado real de su sincronización, no decoración. Sale de datos
/// que ya existen: la última versión acordada con el almacén (la base) frente a la fecha de la nota,
/// la cola de conflictos y el alcance de la sync selectiva.
/// </summary>
public static class NoteSyncSignal
{
    public static SyncSignalState For(Note note, AppSettings settings, SyncBaseVersion? baseVersion, bool hasConflict)
    {
        if (!settings.SyncEnabled) return SyncSignalState.Hidden;
        if (!SyncScopeFilter.Includes(note, settings)) return SyncSignalState.Excluded;
        if (hasConflict) return SyncSignalState.Conflict;
        return baseVersion is not null && baseVersion.UpdatedAt == note.UpdatedAt
            ? SyncSignalState.Synced
            : SyncSignalState.Pending;
    }

    public static string Glyph(SyncSignalState state) => state switch
    {
        SyncSignalState.Synced => "▂▄▆█",
        SyncSignalState.Pending => "▂▄▆_",
        SyncSignalState.Conflict => "▂▄!_",
        SyncSignalState.Excluded => "▂___",
        _ => "",
    };
}
