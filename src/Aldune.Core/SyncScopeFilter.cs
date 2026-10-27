namespace Aldune.Core;

/// <summary>Qué notas entran en la sincronización según el alcance elegido. Un solo sitio para la sync
/// y para la señal de cada nota, que tienen que estar de acuerdo.</summary>
public static class SyncScopeFilter
{
    /// <param name="localOnly">Notas vinculadas a un archivo sin "Sincronizar esta nota": fuera siempre,
    /// sea cual sea el alcance (spec de notas vinculadas, decisión 2).</param>
    public static bool Includes(Note note, AppSettings settings, IReadOnlySet<Guid>? localOnly = null) =>
        Includes(note, settings.SyncScope, settings.SyncNoteIds, settings.SyncTag, localOnly);

    /// <summary>El mismo alcance leído de un perfil de la lista, sin pasar por unos ajustes de trabajo.</summary>
    public static bool Includes(Note note, SyncProfileSettings profile, IReadOnlySet<Guid>? localOnly = null) =>
        Includes(note, profile.SyncScope, profile.SyncNoteIds, profile.SyncTag, localOnly);

    private static bool Includes(Note note, SyncScopeKind scope, IReadOnlyCollection<Guid>? noteIds, string? tag, IReadOnlySet<Guid>? localOnly)
    {
        if (localOnly is not null && localOnly.Contains(note.Id)) return false;
        return scope switch
        {
            SyncScopeKind.SelectedNotes => noteIds?.Contains(note.Id) == true,
            SyncScopeKind.Tag => string.IsNullOrWhiteSpace(tag) ||
                note.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)),
            _ => true,
        };
    }
}
