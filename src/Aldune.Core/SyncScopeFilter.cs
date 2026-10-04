namespace Aldune.Core;

/// <summary>Qué notas entran en la sincronización según el alcance elegido. Un solo sitio para la sync
/// y para la señal de cada nota, que tienen que estar de acuerdo.</summary>
public static class SyncScopeFilter
{
    /// <param name="localOnly">Notas vinculadas a un archivo sin "Sincronizar esta nota": fuera siempre,
    /// sea cual sea el alcance (spec de notas vinculadas, decisión 2).</param>
    public static bool Includes(Note note, AppSettings settings, IReadOnlySet<Guid>? localOnly = null)
    {
        if (localOnly is not null && localOnly.Contains(note.Id)) return false;
        return settings.SyncScope switch
        {
            SyncScopeKind.SelectedNotes => settings.SyncNoteIds.Contains(note.Id),
            SyncScopeKind.Tag => string.IsNullOrWhiteSpace(settings.SyncTag) ||
                note.Tags.Any(tag => string.Equals(tag, settings.SyncTag, StringComparison.OrdinalIgnoreCase)),
            _ => true,
        };
    }
}
