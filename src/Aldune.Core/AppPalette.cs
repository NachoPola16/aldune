namespace Aldune.Core;

/// <summary>Aspecto del chrome de la app (ventanas, dock, avisos). Las notas tienen sus propios temas.
/// Se escribe como número; Dark = 0 para que un settings.json anterior siga en oscuro. Una versión
/// anterior que lea 5–10 (los aspectos retro) cae en Oscuro.</summary>
public enum AppearanceMode
{
    Dark = 0,
    Light = 1,
    /// <summary>Como Windows ("Modo de aplicación predeterminado" en Personalización > Colores).</summary>
    System = 2,
    /// <summary>Claro teñido de lavanda, con acento rosa.</summary>
    Pastel = 3,
    /// <summary>Oscuro frío, azul noche, en vez del marrón cálido de Dark.</summary>
    Midnight = 4,
    /// <summary>Windows XP (Luna): beige, barra azul en degradado vertical, Tahoma.</summary>
    XpLight = 5,
    /// <summary>Windows 95 con la barra de Windows 98 y el acento de XP, en grafito oscuro.</summary>
    XpDark = 6,
    /// <summary>Papel de instrumento de laboratorio: tinta, líneas de 1 px, canales.</summary>
    TelecomLight = 7,
    /// <summary>Pantalla de osciloscopio con retícula.</summary>
    TelecomDark = 8,
    /// <summary>Terminal de Linux (Gruvbox por defecto).</summary>
    Bash = 9,
    /// <summary>Monitor de fósforo antiguo, monocromo.</summary>
    Phosphor = 10
}

/// <summary>
/// Paleta del chrome, oscura y clara, en un solo sitio: la capa WPF la convierte en pinceles
/// <c>Aldune{Clave}Brush</c> y el código que pinta a mano (bandeja, avisos) la lee de aquí. Las
/// claves son semánticas (texto, superficie, peligro…), no tonos, para que las dos paletas se
/// correspondan una a una. Los tests fijan el contraste mínimo de cada texto sobre sus fondos.
/// </summary>
public static class AppPalette
{
    private static readonly IReadOnlyDictionary<string, string> Dark = new Dictionary<string, string>
    {
        ["Ground"] = "#2A261F",
        ["GroundDeep"] = "#25211B",
        ["Surface"] = "#332F28",
        ["Popup"] = "#2D2923",
        ["Raised"] = "#3C3730",
        ["RaisedHover"] = "#403B33",
        ["Hover"] = "#4E4840",
        ["Selected"] = "#443F36",
        ["Pressed"] = "#5F584E",
        ["Track"] = "#4A443B",
        ["Divider"] = "#3C3730",
        ["Text"] = "#EDE7DC",
        ["TextStrong"] = "#F5F0E6",
        ["TextSoft"] = "#C9C0B2",
        ["TextWarm"] = "#C8B9A8",
        ["MutedText"] = "#A79E90",
        ["Hint"] = "#8A8175",
        ["TextFaint"] = "#6B6459",
        ["TextDisabled"] = "#71695E",
        ["BorderSubtle"] = "#5A5146",
        ["Border"] = "#62594D",
        ["FieldHover"] = "#8A8175",
        ["FieldFocus"] = "#A18465",
        ["AccentBorder"] = "#B39A7E",
        ["Accent"] = "#A18465",
        ["AccentHover"] = "#B79570",
        ["OnAccent"] = "#201B16",
        ["Selection"] = "#806A55",
        ["DangerBg"] = "#5A2A28",
        ["DangerStrong"] = "#6B302C",
        ["DangerStrongHover"] = "#823A35",
        ["OnDanger"] = "#F5E6E3",
        ["DangerText"] = "#E8A0A0",
        ["ErrorText"] = "#E0A08A",
        ["ErrorBorder"] = "#D98C84",
        ["Warning"] = "#E0B070",
        ["Success"] = "#A9C99A",
        ["Notice"] = "#E0CBA8",
        ["DockBar"] = "#B52A241E",
        ["Hairline"] = "#40FFFFFF",
        ["ScrollThumbBorder"] = "#D0B28E",
        ["ScrollThumbDragBorder"] = "#E0C6A4",
        ["DangerHover"] = "#5A2E2E",
        ["OnDangerBg"] = "#F0C1BA",
        ["Attention"] = "#C9A46C",
        ["DockPrimary"] = "#575044",
        ["DockArrowText"] = "#E8D8C5",
        ["DockArrowBorder"] = "#D08F7965",
        ["DockArrowHover"] = "#D14A3D31",
        ["DockArrowHoverBorder"] = "#F0C7A5",
        ["DockArrowPressed"] = "#E05B4939",
        ["DockRailBorder"] = "#C08B7461",
        ["DockThumb"] = "#E1C2A88F",
        ["DockThumbBorder"] = "#F2DCC2",
        // Piel (bloque 2). Barra = fondo y sin relieve visible: el oscuro de siempre no cambia. Los
        // aspectos retro dan su propio valor a cada una. CH3 y CH4, más claros que los de las
        // maquetas: su título va sobre su propio tinte y con los de la maqueta no llegaba a 4.5:1.
        ["TitleBar"] = "#2A261F",
        ["TitleBarEnd"] = "#2A261F",
        ["OnTitleBar"] = "#F5F0E6",
        ["BevelLight"] = "#5F584E",
        ["BevelDark"] = "#15120E",
        ["Channel1"] = "#F2D338",
        ["Channel2"] = "#3FD0E0",
        ["Channel3"] = "#EA80DD",
        ["Channel4"] = "#7FA8FF",
        ["PromptUser"] = "#A9C99A",
        ["PromptPath"] = "#9FB8D0",
    };

    // Papel cálido, no blanco puro: el mismo tono tostado del oscuro llevado a claro, para que las
    // notas pastel no queden sobre un blanco que las apaga.
    private static readonly IReadOnlyDictionary<string, string> Light = new Dictionary<string, string>
    {
        ["Ground"] = "#F6F1E8",
        ["GroundDeep"] = "#EFE9DE",
        ["Surface"] = "#ECE5D9",
        ["Popup"] = "#FBF8F2",
        ["Raised"] = "#E2DACB",
        ["RaisedHover"] = "#DCD3C3",
        ["Hover"] = "#D4CAB8",
        ["Selected"] = "#D8CEBD",
        ["Pressed"] = "#C8BDA9",
        ["Track"] = "#D0C6B4",
        ["Divider"] = "#E2DACB",
        ["Text"] = "#2A261F",
        ["TextStrong"] = "#1E1A14",
        ["TextSoft"] = "#4A443B",
        ["TextWarm"] = "#5C5044",
        ["MutedText"] = "#655E53",
        ["Hint"] = "#766D61",
        ["TextFaint"] = "#978D80",
        ["TextDisabled"] = "#A39A8D",
        ["BorderSubtle"] = "#D0C6B5",
        ["Border"] = "#BFB4A1",
        ["FieldHover"] = "#A89D8B",
        ["FieldFocus"] = "#8C6B4B",
        ["AccentBorder"] = "#9A7B5B",
        ["Accent"] = "#8C6B4B",
        ["AccentHover"] = "#7A5C3E",
        ["OnAccent"] = "#FFF9F0",
        ["Selection"] = "#D6C0A3",
        ["DangerBg"] = "#F0D5D0",
        ["DangerStrong"] = "#B5473E",
        ["DangerStrongHover"] = "#9E3C34",
        ["OnDanger"] = "#FFFFFF",
        ["DangerText"] = "#A8453B",
        ["ErrorText"] = "#A2502F",
        ["ErrorBorder"] = "#C0655B",
        ["Warning"] = "#9A6B1F",
        ["Success"] = "#4E7A3E",
        ["Notice"] = "#7A5C2E",
        ["DockBar"] = "#E6F0EAE0",
        ["Hairline"] = "#33000000",
        ["ScrollThumbBorder"] = "#7A5C3E",
        ["ScrollThumbDragBorder"] = "#6A4E33",
        ["DangerHover"] = "#F0D5D0",
        ["OnDangerBg"] = "#8A2F28",
        ["Attention"] = "#80591A",
        ["DockPrimary"] = "#E2DACB",
        ["DockArrowText"] = "#4A3F33",
        ["DockArrowBorder"] = "#D0A89880",
        ["DockArrowHover"] = "#E6E2D8C8",
        ["DockArrowHoverBorder"] = "#8C6B4B",
        ["DockArrowPressed"] = "#F0D2C6B2",
        ["DockRailBorder"] = "#C0B5A58F",
        ["DockThumb"] = "#E0A08C74",
        ["DockThumbBorder"] = "#6A5A48",
        // Piel (bloque 2), como en Dark. Canales oscurecidos para el papel claro: el título de una
        // pestaña con tinte va en su color sobre ese tinte y tiene que llegar a 4.5:1.
        ["TitleBar"] = "#F6F1E8",
        ["TitleBarEnd"] = "#F6F1E8",
        ["OnTitleBar"] = "#1E1A14",
        ["BevelLight"] = "#FFFFFF",
        ["BevelDark"] = "#978D80",
        ["Channel1"] = "#6E5600",
        ["Channel2"] = "#00636C",
        ["Channel3"] = "#9C1F6E",
        ["Channel4"] = "#1F57B5",
        ["PromptUser"] = "#44703A",
        ["PromptPath"] = "#2F5F86",
    };

    public static IReadOnlyDictionary<string, string> For(bool light) => light ? Light : Dark;

    public static IReadOnlyDictionary<string, string> For(AppearanceMode mode, bool windowsUsesLight) => mode switch
    {
        AppearanceMode.Pastel => Pastel.Value,
        AppearanceMode.Midnight => Midnight.Value,
        _ => For(IsLight(mode, windowsUsesLight))
    };

    public static bool IsLight(AppearanceMode mode, bool windowsUsesLight) => mode switch
    {
        AppearanceMode.Light or AppearanceMode.Pastel or AppearanceMode.XpLight or AppearanceMode.TelecomLight => true,
        AppearanceMode.System => windowsUsesLight,
        _ => false
    };

    // Las variantes se derivan de Light y Dark en vez de escribirse a mano: se conserva la claridad
    // OKLCH de cada clave (de ella sale casi todo el contraste) y solo se tiñen. Así heredan el
    // contraste ya comprobado, y una clave nueva en Light/Dark llega sola a las variantes.
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Pastel =
        new(() => Tint(Light, neutralHue: 300, neutralChroma: 0.022, accentHue: 350));

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Midnight =
        new(() => Tint(Dark, neutralHue: 255, neutralChroma: 0.03, accentHue: 235));

    /// <summary>Colores con significado propio (peligro, error, aviso, éxito; los canales del
    /// osciloscopio y los del prompt): no se tiñen, un "borrar" tiene que seguir pareciendo peligroso
    /// y CH1 tiene que seguir siendo amarillo en cualquier paleta.</summary>
    private static readonly HashSet<string> Semantic =
    [
        "DangerBg", "DangerStrong", "DangerStrongHover", "OnDanger", "DangerText", "DangerHover",
        "OnDangerBg", "ErrorText", "ErrorBorder", "Warning", "Success", "Notice", "Attention",
        "Channel1", "Channel2", "Channel3", "Channel4", "PromptUser", "PromptPath",
    ];

    private static IReadOnlyDictionary<string, string> Tint(
        IReadOnlyDictionary<string, string> source, double neutralHue, double neutralChroma, double accentHue)
    {
        var result = new Dictionary<string, string>();
        foreach (var (token, value) in source)
        {
            // #AARRGGBB: el alfa se conserva, se tiñe el color.
            string alpha = value.Length == 9 ? value.Substring(1, 2) : "";
            string rgb = "#" + value.Substring(value.Length - 6);
            if (Semantic.Contains(token) || !OklchColor.TryFromHex(rgb, out var color))
            {
                result[token] = value;
                continue;
            }

            // Casi gris (fondos, textos, bordes): toma el matiz de la paleta con poco croma. Con
            // color de verdad (acentos): cambia de matiz y conserva su croma.
            var tinted = color.C < 0.045
                ? color with { C = neutralChroma, H = neutralHue }
                : color with { H = accentHue };
            var hex = tinted.ToHex();
            result[token] = alpha.Length > 0 ? "#" + alpha + hex.Substring(1) : hex;
        }
        return result;
    }
}
