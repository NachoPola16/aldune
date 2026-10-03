namespace Aldune.Core;

/// <summary>Qué notas entran en la sincronización según el alcance elegido. Un solo sitio para la sync
/// y para la señal de cada nota, que tienen que estar de acuerdo.</summary>
public static class SyncScopeFilter
{
    public static bool Includes(Note note, AppSettings settings) => settings.SyncScope switch
    {
        SyncScopeKind.SelectedNotes => settings.SyncNoteIds.Contains(note.Id),
        SyncScopeKind.Tag => string.IsNullOrWhiteSpace(settings.SyncTag) ||
            note.Tags.Any(tag => string.Equals(tag, settings.SyncTag, StringComparison.OrdinalIgnoreCase)),
        _ => true,
    };
}
