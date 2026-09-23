namespace Aldune.Windowing;

/// <summary>
/// Colores del chrome y acceso WPF a los colores derivados de una nota. Las paletas de notas ya no
/// viven aquí: son temas (<see cref="Aldune.Core.NoteThemes"/>) y el borde y la etiqueta se
/// calculan para cualquier color (<see cref="Aldune.Core.NoteColorDerivation"/>).
/// </summary>
internal static class NoteColorPalette
{
    /// <summary>Tinta del texto sobre una nota clara. Negro tintado hacia el cálido, nunca #000.</summary>
    internal const string Ink = Aldune.Core.NoteColorContrast.Ink;

    /// <summary>Fondo del chrome (dock, gestor). Neutro tintado, nunca el #3A3A3A plano.</summary>
    internal const string Ground = "#2A261F";

    /// <summary>Un escalón por encima de <see cref="Ground"/>, para controles sobre él.</summary>
    internal const string GroundRaised = "#3C3730";

    internal static string RimFor(string color) => Aldune.Core.NoteColorDerivation.RimFor(color);

    internal static string LabelFor(string color) => Aldune.Core.NoteColorDerivation.LabelFor(color);

    /// <summary>Adaptive foreground keeps light and dark custom colors readable.</summary>
    internal static string ForegroundFor(string color) => Aldune.Core.NoteColorContrast.ForegroundFor(color);

    internal static bool IsReadableCustom(string color) =>
        Aldune.Core.NoteColorContrast.IsReadable(color, ForegroundFor(color));
}
