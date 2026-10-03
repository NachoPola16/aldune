using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// Canal de cada nota (piel de osciloscopio) para los convertidores de XAML, que no reciben el mazo.
/// Mismo patrón que <see cref="NoteColorDisplay"/>. Sale del orden del mazo completo, sin el filtro de
/// etiqueta: filtrar no puede cambiar de canal una nota, ni hacer que su ventana y su pestaña discrepen.
/// </summary>
internal static class NoteChannelDisplay
{
    private static Dictionary<Guid, int> _positions = [];

    public static void Set(IEnumerable<Note> ordered) =>
        _positions = ordered.Select((note, index) => (note.Id, index)).ToDictionary(entry => entry.Id, entry => entry.index);

    public static int Of(Guid noteId) => _positions.TryGetValue(noteId, out var position) ? NoteChannels.Of(position) : 0;
}
