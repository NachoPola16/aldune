namespace Aldune.Core;

/// <summary>Bordes del chrome: planos (hoy) o con el relieve de Windows 95 (claro arriba-izquierda,
/// oscuro abajo-derecha).</summary>
public enum SkinBorder
{
    Flat = 0,
    Bevel = 1,
}

/// <summary>Barra de título de las notas (y fondo de la cabecera de Ajustes y del gestor): sin barra
/// propia, como hoy, o en degradado vertical (XP) u horizontal (Windows 98).</summary>
public enum SkinTitleBar
{
    Plain = 0,
    GradientVertical = 1,
    GradientHorizontal = 2,
}

/// <summary>Cómo lleva cada nota su color en el dock y en su ventana. Ver <see cref="NoteFace"/>.</summary>
public enum SkinCard
{
    /// <summary>La nota entera de su color (hoy).</summary>
    Filled = 0,
    /// <summary>Fondo del chrome y el color solo en una franja de 3 px (terminal).</summary>
    Stripe = 1,
    /// <summary>Tinte suave del color de su canal, por puesto en el dock (osciloscopio).</summary>
    Tinted = 2,
}

/// <summary>Adorno de los títulos en el dock. Solo se pinta: el texto de la nota no cambia.</summary>
public enum SkinTitleAdornment
{
    /// <summary>Mayúsculas espaciadas (hoy).</summary>
    None = 0,
    /// <summary>Prefijo de canal: <c>CH1 HOY</c>.</summary>
    Channel = 1,
    /// <summary>Sufijo de carpeta: <c>hoy/</c>.</summary>
    Folder = 2,
    /// <summary>Mayúsculas sin espaciar.</summary>
    Uppercase = 3,
}

/// <summary>
/// La forma de un aspecto, frente a su paleta (<see cref="AppPalette"/>): tipografía, bordes, barra de
/// título, cómo lleva cada nota su color y los adornos. Datos puros; la capa WPF los convierte en
/// recursos y en el estado que leen las plantillas. No se guarda en settings.json: sale del aspecto.
/// </summary>
public sealed record AppSkin(
    string ChromeFont,
    string NoteFont,
    string NoteTitleFont,
    double NoteTitleFontSize,
    SkinBorder Border,
    SkinTitleBar TitleBar,
    SkinCard Card,
    SkinTitleAdornment Adornment,
    bool PromptLine,
    bool NoteGrid)
{
    /// <summary>Lo de siempre: Segoe en el chrome y el cuerpo, título manuscrito, todo plano.</summary>
    public static AppSkin Default { get; } = new(
        ChromeFont: "Segoe UI Variable Text, Segoe UI",
        NoteFont: "Segoe UI Variable Text, Segoe UI",
        NoteTitleFont: "Ink Free, Segoe UI Variable Text",
        NoteTitleFontSize: 17,
        Border: SkinBorder.Flat,
        TitleBar: SkinTitleBar.Plain,
        Card: SkinCard.Filled,
        Adornment: SkinTitleAdornment.None,
        PromptLine: false,
        NoteGrid: false);

    /// <summary>La piel de cada aspecto. Los de hoy usan la de siempre; los retro llegan en el bloque 3.</summary>
    public static AppSkin For(AppearanceMode mode) => Default;
}
