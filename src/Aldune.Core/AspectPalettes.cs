namespace Aldune.Core;

/// <summary>
/// Paletas de los aspectos retro. Se derivan de Oscuro o Claro como Pastel y Medianoche (se conserva la
/// claridad de cada clave y se cambia el matiz, <see cref="AppPalette.Tint"/>), así que heredan su
/// contraste ya comprobado; encima van los colores del usuario, ajustados con <see cref="ColorFit"/>
/// cuando no dejarían leer el texto. Los tests de contraste corren sobre cada aspecto con colores extremos.
/// </summary>
internal static class AspectPalettes
{
    // Fondos que se mueven juntos cuando el usuario elige el fondo (panel de osciloscopio, terminal).
    private static readonly string[] Backgrounds =
    [
        "Ground", "GroundDeep", "Surface", "Popup", "Raised", "RaisedHover", "Hover", "Selected", "Pressed",
        "Track", "Divider", "TitleBar", "TitleBarEnd", "DockPrimary",
    ];

    private const string White = "#FFFFFF";

    public static IReadOnlyDictionary<string, string> Derive(AppearanceMode mode, IReadOnlyDictionary<string, string> c) => mode switch
    {
        AppearanceMode.XpLight => XpLight(c["titleBar"], c["accent"]),
        AppearanceMode.XpDark => XpDark(c["titleBar"], c["accent"]),
        AppearanceMode.TelecomLight => TelecomLight(c["ink"], c["accent"]),
        AppearanceMode.TelecomDark => TelecomDark(c["panel"], c["trace"]),
        AppearanceMode.Bash => Bash(c["background"], c["user"], c["path"], c["accent"]),
        AppearanceMode.Phosphor => Phosphor(c["phosphor"], c["details"]),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    private static Dictionary<string, string> XpLight(string bar, string accent)
    {
        // El beige de XP (#ECE9D8): el claro de siempre teñido hacia el amarillo grisáceo.
        var p = new Dictionary<string, string>(AppPalette.Tint(AppPalette.Light, neutralHue: 95, neutralChroma: 0.018, accentHue: Hue(accent)));
        SetAccent(p, accent);
        // Degradado vertical de Luna: arriba más claro, abajo el color elegido; texto blanco sobre los dos.
        p["TitleBar"] = ColorFit.Background(ColorFit.Lighter(bar, 0.08), White, 4.5);
        p["TitleBarEnd"] = ColorFit.Background(bar, White, 4.5);
        p["OnTitleBar"] = White;
        p["BevelLight"] = White;
        p["BevelDark"] = "#ACA899";
        return p;
    }

    private static Dictionary<string, string> XpDark(string bar, string accent)
    {
        // Grafito neutro, sin el marrón/naranja de Royale Noir (decidido con el usuario).
        var p = new Dictionary<string, string>(AppPalette.Tint(AppPalette.Dark, neutralHue: 255, neutralChroma: 0.012, accentHue: Hue(accent)));
        SetAccent(p, accent);
        // Degradado horizontal de Windows 98: oscuro a la izquierda, el color elegido más claro a la derecha.
        p["TitleBar"] = ColorFit.Background(ColorFit.Darker(bar, 0.12), White, 4.5);
        p["TitleBarEnd"] = ColorFit.Background(ColorFit.Lighter(bar, 0.08), White, 4.5);
        p["OnTitleBar"] = White;
        p["BevelLight"] = "#6A6E76";
        p["BevelDark"] = "#121316";
        return p;
    }

    private static Dictionary<string, string> TelecomLight(string ink, string accent)
    {
        // Papel de laboratorio: el claro de siempre con un gris verdoso apenas teñido; la tinta elegida
        // es el texto y tiene que leerse sobre el más oscuro de los fondos de texto (Raised).
        var p = new Dictionary<string, string>(AppPalette.Tint(AppPalette.Light, neutralHue: NeutralHue(ink, 100), neutralChroma: 0.012, accentHue: Hue(accent)));
        // Ajuste de los tests de contraste con tinta blanca o amarilla: ajustar solo contra Raised dejaba
        // el texto en 3.89:1 sobre Hover y en 4.50 (redondeado a la baja) sobre DangerBg, que son más
        // oscuros. Ahora se ajusta contra todos los fondos donde se lee el texto, de menos a más oscuro.
        var text = FitText(ink, p);
        p["Text"] = p["TextStrong"] = p["OnTitleBar"] = text;
        SetAccent(p, accent);
        return p;
    }

    private static Dictionary<string, string> TelecomDark(string panel, string trace)
    {
        var p = new Dictionary<string, string>(AppPalette.Tint(AppPalette.Dark, neutralHue: NeutralHue(panel, 240), neutralChroma: NeutralChroma(panel, 0.02), accentHue: Hue(trace)));
        MoveBackgrounds(p, panel);
        // La traza es CH1 y el acento: un amarillo de osciloscopio que se lee sobre el panel.
        p["Channel1"] = ColorFit.Foreground(trace, p["Raised"], 4.5);
        SetAccent(p, trace);
        return p;
    }

    private static Dictionary<string, string> Bash(string background, string user, string path, string accent)
    {
        var p = new Dictionary<string, string>(AppPalette.Tint(AppPalette.Dark, neutralHue: NeutralHue(background, 60), neutralChroma: NeutralChroma(background, 0.03), accentHue: Hue(accent)));
        MoveBackgrounds(p, background);
        p["PromptUser"] = ColorFit.Foreground(user, p["Ground"], 4.5);
        p["PromptPath"] = ColorFit.Foreground(path, p["Ground"], 4.5);
        // Como en la maqueta: botón del color del acento con el texto del color de la terminal.
        p["OnAccent"] = p["Ground"];
        SetAccent(p, accent);
        return p;
    }

    private static Dictionary<string, string> Phosphor(string phosphor, string details)
    {
        var p = new Dictionary<string, string>(AppPalette.Tint(AppPalette.Dark, neutralHue: NeutralHue(phosphor, 150), neutralChroma: NeutralChroma(phosphor, 0.025), accentHue: Hue(details)));
        // Fondos casi negros del mismo matiz que el fósforo, como el cristal de un monitor apagado.
        MoveBackgrounds(p, ColorMix.Toward(phosphor, "#000000", 0.9));
        // Con fósforo negro, ajustar solo contra Raised dejaba el texto en 3.00:1 sobre DangerBg (un rojo
        // oscuro que no se mueve): FitText lo ajusta contra todos los fondos de texto, Hover y DangerBg.
        var text = FitText(phosphor, p);
        p["Text"] = p["TextStrong"] = p["OnTitleBar"] = text;
        p["TextSoft"] = p["TextWarm"] = ColorFit.Foreground(ColorMix.Toward(text, p["Ground"], 0.15), p["Raised"], 4.5);
        p["OnAccent"] = p["Ground"];
        SetAccent(p, details);
        return p;
    }

    // El acento y sus acompañantes, con el texto del botón (OnAccent) legible encima. También el botón "+"
    // del dock, que en las maquetas va del color del acento: su glifo es TextStrong, así que se llama
    // después de fijar TextStrong.
    private static void SetAccent(Dictionary<string, string> p, string accent)
    {
        var onAccent = p["OnAccent"];
        var fitted = ColorFit.Background(accent, onAccent, 4.5);
        bool darkText = ColorFit.Ratio(onAccent, "#000000") < ColorFit.Ratio(onAccent, White);
        p["DockPrimary"] = ColorFit.Background(accent, p["TextStrong"], 4.5);
        p["Accent"] = p["AccentBorder"] = p["FieldFocus"] = fitted;
        // Al pasar el ratón, un paso más lejos del texto: más claro bajo texto oscuro, más oscuro bajo claro.
        p["AccentHover"] = ColorFit.Background(darkText ? ColorFit.Lighter(fitted, 0.05) : ColorFit.Darker(fitted, 0.05), onAccent, 4.5);
    }

    /// <summary>
    /// Pone el fondo elegido como Ground y mueve todos los fondos lo mismo en claridad, para que sigan
    /// escalonados. La claridad se limita a [0.14, 0.30]: más claro, los textos del oscuro dejarían de
    /// leerse; más oscuro, los escalones se pierden contra el negro.
    /// </summary>
    private static void MoveBackgrounds(Dictionary<string, string> p, string ground)
    {
        if (!OklchColor.TryFromHex(ground, out var chosen) || !OklchColor.TryFromHex(p["Ground"], out var current)) return;
        double target = Math.Clamp(chosen.L, 0.14, 0.30);
        double delta = target - current.L;
        foreach (var key in Backgrounds)
        {
            if (!p.TryGetValue(key, out var value) || value.Length != 7 || !OklchColor.TryFromHex(value, out var color)) continue;
            p[key] = (color with { L = Math.Clamp(color.L + delta, 0, 1) }).ToHex();
        }
        p["Ground"] = (chosen with { L = target }).ToHex();
        p["TitleBar"] = p["TitleBarEnd"] = p["Ground"];
        // Ajuste de los tests de contraste con fondo blanco o amarillo (la claridad llega al tope de
        // 0.30): MutedText quedaba en 4.47-4.50 sobre Surface, que sube con Ground; Hint se protege igual.
        p["MutedText"] = ColorFit.Foreground(ColorFit.Foreground(p["MutedText"], p["Ground"], 4.5), p["Surface"], 4.5);
        p["Hint"] = ColorFit.Foreground(p["Hint"], p["Ground"], 3.5);
    }

    // El texto principal se lee sobre estos fondos (los que fijan los tests de contraste). Se ajusta uno
    // tras otro: cada ajuste solo aleja el color del fondo, así que los anteriores se siguen cumpliendo.
    private static readonly string[] TextBackgrounds = ["Ground", "Surface", "Raised", "Popup", "Hover", "DangerBg"];

    private static string FitText(string text, Dictionary<string, string> p)
    {
        foreach (var key in TextBackgrounds)
            text = ColorFit.Foreground(text, p[key], 4.5);
        return text;
    }

    private static double Hue(string color) => OklchColor.TryFromHex(color, out var c) ? c.H : 0;

    // Un color sin matiz (gris, blanco) no puede teñir la paleta de un matiz inventado: su "matiz" es ruido.
    private static double NeutralChroma(string color, double maximum) =>
        OklchColor.TryFromHex(color, out var c) && c.C >= 0.02 ? Math.Min(c.C, maximum) : 0;

    private static double NeutralHue(string color, double fallback) =>
        OklchColor.TryFromHex(color, out var c) && c.C >= 0.02 ? c.H : fallback;
}
