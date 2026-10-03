using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// Color único activo para pintar las notas, visible para los convertidores de XAML. Estático por el
/// mismo motivo que <see cref="NoteSnippetConverter.Enabled"/>: los convertidores los crea XAML y no
/// reciben los ajustes. Lo fijan el arranque y Ajustes; después hay que llamar a
/// <c>AppCoordinator.RefreshNoteAppearance</c> para repintar.
/// </summary>
internal static class NoteColorDisplay
{
    public static string? Uniform { get; set; }

    public static string Resolve(string? color) => NoteDisplayColor.Resolve(color ?? string.Empty, Uniform);
}
