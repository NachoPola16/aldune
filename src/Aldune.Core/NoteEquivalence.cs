using System.Text.Json;

namespace Aldune.Core;

/// <summary>
/// ¿Dos versiones de una nota dicen lo mismo? Para la sincronización: dos equipos que hacen el mismo
/// cambio (la migración de temas recolorea igual en cada uno) llegan con fechas distintas, y por fechas
/// eso es un conflicto aunque no haya nada que elegir. Compara lo que el usuario ve y edita; no la
/// fecha ni el dispositivo que firmó la versión.
/// </summary>
public static class NoteEquivalence
{
    public static bool SameContent(Note? a, Note? b)
    {
        if (a is null || b is null) return false;

        return a.Text == b.Text
            && string.Equals(a.Color, b.Color, StringComparison.OrdinalIgnoreCase)
            && a.State == b.State
            && a.DockPosition == b.DockPosition
            && a.IsProtected == b.IsProtected
            && a.Tags.OrderBy(tag => tag, StringComparer.Ordinal)
                .SequenceEqual(b.Tags.OrderBy(tag => tag, StringComparer.Ordinal), StringComparer.Ordinal)
            && SameProtectedContent(a.ProtectedContent, b.ProtectedContent);
    }

    // El contenido protegido va cifrado con su propia sal: dos copias del mismo cifrado serializan
    // igual; dos cifrados distintos del mismo texto no, y eso está bien (no se puede saber sin la
    // contraseña, así que se trata como cambio real).
    private static bool SameProtectedContent(ProtectedNoteContent? a, ProtectedNoteContent? b) =>
        a is null || b is null
            ? a is null && b is null
            : JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);
}
