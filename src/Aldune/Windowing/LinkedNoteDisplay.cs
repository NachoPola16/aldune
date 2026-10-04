using System.IO;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// Qué notas son archivos, para los convertidores de XAML y las ventanas (que no reciben el repositorio).
/// Mismo patrón que <see cref="NoteChannelDisplay"/>: lo rellena el dock al refrescarse.
/// </summary>
internal static class LinkedNoteDisplay
{
    private static Dictionary<Guid, string> _paths = [];
    // Lo lee el temporizador del sondeo (otro hilo) y lo escribe la interfaz: un HashSet se corrompería.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> Unavailable = new();

    public static void Set(IEnumerable<NoteFileLink> links) =>
        _paths = links.ToDictionary(link => link.NoteId, link => link.Path);

    public static bool IsLinked(Guid noteId) => _paths.ContainsKey(noteId);

    public static string? PathOf(Guid noteId) => _paths.GetValueOrDefault(noteId);

    public static bool IsUnavailable(Guid noteId) => Unavailable.ContainsKey(noteId);

    public static void SetUnavailable(Guid noteId, bool unavailable)
    {
        if (unavailable) Unavailable[noteId] = 0;
        else Unavailable.TryRemove(noteId, out _);
    }

    /// <summary>El título de siempre; si la primera línea está vacía y la nota es un archivo, su nombre.</summary>
    public static string Title(Note note) => Title(note.Id, note.Text);

    public static string Title(Guid noteId, string text)
    {
        var title = NoteTitleHelper.GetTitle(text);
        return title == NoteTitleHelper.PlaceholderTitle && PathOf(noteId) is { } path
            ? Path.GetFileNameWithoutExtension(path)
            : title;
    }
}
