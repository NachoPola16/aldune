namespace Aldune.Core;

/// <summary>
/// Un conjunto de colores de nota pensados para ir juntos: tonos oscuros (con tinta clara) y tonos
/// claros (con tinta oscura), cada lista en su orden de rotación. Puede faltar una de las dos.
/// Get/set y listas mutables porque los temas propios se guardan tal cual en settings.json.
/// </summary>
public sealed class NoteTheme
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> DarkColors { get; set; } = new();
    public List<string> LightColors { get; set; } = new();

    /// <summary>Los de serie no se editan ni se borran; se duplican.</summary>
    public bool IsBuiltIn { get; set; }
}

/// <summary>De qué tono nacen las notas nuevas.</summary>
public enum NoteTone
{
    Light,
    Dark,

    /// <summary>Alternando: oscura tras una clara y clara tras una oscura.</summary>
    Both,
}

/// <summary>Cómo se elige el color de una nota nueva dentro del tema.</summary>
public enum NoteColorAssignment
{
    /// <summary>En el orden del tema, saltando los colores de las notas de al lado.</summary>
    RotateAvoidNeighbors,
    Rotate,
    MostDistinct,
    Fixed,
}
