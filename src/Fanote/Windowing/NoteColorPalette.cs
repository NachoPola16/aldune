namespace Fanote.Windowing;

/// <summary>
/// Paleta de notas, derivada en OKLCH y no a ojo en hex.
///
/// Las seis caras comparten exactamente L=0.87, con el croma acotado hue a hue al máximo que
/// sRGB puede representar (iris y amber no llegan a 0.095, así que se quedan en su máximo real en
/// vez de salirse de gamut y clipear). Esto importa: la paleta anterior mezclaba claridades
/// dispares, así que unas notas "pesaban" más que otras sin que eso significase nada — el color
/// es identidad, no jerarquía, y ninguna nota debe gritar más que otra.
///
/// Cada cara lleva un borde (<see cref="Rims"/>) del mismo hue y croma a L-0.16. Es la única
/// forma de dar volumen a una pestaña aquí: la spec v1 descarta AllowsTransparency (rompe
/// ClearType), así que todo píxel dentro de la región es opaco y no hay sombras posibles.
///
/// Todas las caras dan &gt;10:1 de contraste contra <see cref="Ink"/>, muy por encima de AA.
/// </summary>
internal static class NoteColorPalette
{
    /// <summary>Tinta del texto sobre una nota. Negro tintado hacia el cálido, nunca #000.</summary>
    internal const string Ink = "#1E1A14";

    /// <summary>Fondo del chrome (dock, gestor). Neutro tintado, nunca el #3A3A3A plano.</summary>
    internal const string Ground = "#2A261F";

    /// <summary>Un escalón por encima de <see cref="Ground"/>, para controles sobre él.</summary>
    internal const string GroundRaised = "#3C3730";

    internal static readonly string[] Colors =
    {
        "#EBD38B", // citron  oklch(0.87 0.095 92)
        "#AAE6B1", // sage    oklch(0.87 0.095 148)
        "#83E7F2", // sky     oklch(0.87 0.095 205)
        "#C2D4FF", // iris    oklch(0.87 0.063 268)  croma tope de gamut
        "#F9BEF2", // rose    oklch(0.87 0.095 330)
        "#FFC5AF", // amber   oklch(0.87 0.073 42)   croma tope de gamut
    };

    /// <summary>
    /// Color de la etiqueta del lomo, en el mismo orden que <see cref="Colors"/>. Mismo hue a
    /// L-0.44.
    ///
    /// Es una escala aparte de <see cref="Rims"/> y no un reaprovechamiento suyo: el borde se
    /// diseñó como un filete de 1px y da 1.74:1 contra su cara, que para una línea está bien pero
    /// para texto de 11px en mayúsculas es ilegible. Verificado rasterizando la plantilla de
    /// pestaña en aislamiento antes de darla por buena. Estos tonos dan 5.3-5.6:1, por encima del
    /// 4.5:1 que pide AA para texto normal (11px SemiBold no cuenta como texto grande).
    /// </summary>
    internal static readonly string[] Labels =
    {
        "#614E00", // citron
        "#006123", // sage
        "#005B63", // sky
        "#3A4C83", // iris
        "#743170", // rose
        "#7D391D", // amber
    };

    /// <summary>Borde de cada color, en el mismo orden que <see cref="Colors"/>.</summary>
    internal static readonly string[] Rims =
    {
        "#B7A059",
        "#78B280",
        "#4CB3BE",
        "#90A1CA",
        "#C38CBE",
        "#C9937E",
    };

    /// <summary>
    /// El borde que corresponde a una cara.
    ///
    /// Para los seis colores de la paleta devuelve su <see cref="Rims"/> calculado en OKLCH. Para
    /// cualquier otro lo oscurece proporcionalmente: las notas creadas por versiones anteriores de
    /// la app siguen guardadas en la base de datos con los hex viejos, y sin este camino se
    /// quedarían sin borde y se verían planas al lado de las nuevas. Se hace así, y no migrando
    /// la base de datos a la paleta nueva, porque el color de una nota es una elección del
    /// usuario: reasignarlo en silencio cambiaría sus datos sin pedírselo.
    /// </summary>
    internal static string RimFor(string color)
    {
        int index = Array.IndexOf(Colors, color);
        if (index >= 0) return Rims[index];
        return Darken(color, 0.72) ?? color;
    }

    /// <summary>
    /// El color de etiqueta que corresponde a una cara. Mismo trato que <see cref="RimFor"/> para
    /// las notas heredadas, pero oscureciendo bastante más: aquí el resultado tiene que ser
    /// legible como texto, no solo distinguirse como línea.
    /// </summary>
    internal static string LabelFor(string color)
    {
        int index = Array.IndexOf(Colors, color);
        if (index >= 0) return Labels[index];
        return Darken(color, 0.42) ?? Ink;
    }

    /// <summary>
    /// The note editor uses the same warm-dark ink for every note. Custom colors therefore need
    /// to stay in the light range; accepting a dark color here would technically work but would
    /// make the title and body unreadable. The six built-in colors are already designed to pass.
    /// </summary>
    internal static bool IsReadableCustom(string color)
    {
        if (!TryGetRgb(color, out var r, out var g, out var b)) return false;

        var backgroundLuminance = RelativeLuminance(r, g, b);
        var inkLuminance = RelativeLuminance(0x1E, 0x1A, 0x14);
        var contrast = (Math.Max(backgroundLuminance, inkLuminance) + 0.05)
            / (Math.Min(backgroundLuminance, inkLuminance) + 0.05);
        return contrast >= 4.5;
    }

    private static bool TryGetRgb(string color, out int r, out int g, out int b)
    {
        r = g = b = 0;
        if (color.Length != 7 || color[0] != '#') return false;
        if (!int.TryParse(color.AsSpan(1), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out int rgb))
        {
            return false;
        }

        r = (rgb >> 16) & 0xFF;
        g = (rgb >> 8) & 0xFF;
        b = rgb & 0xFF;
        return true;
    }

    private static double RelativeLuminance(int r, int g, int b)
    {
        static double Channel(int value)
        {
            var channel = value / 255.0;
            return channel <= 0.03928
                ? channel / 12.92
                : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
    }

    /// <summary>
    /// Escala los tres canales de un <c>#RRGGBB</c>. Aproxima la caída de L-0.16 que usan los
    /// bordes de la paleta, sin meter una conversión OKLCH completa en tiempo de ejecución por un
    /// caso que solo afecta a notas heredadas. Devuelve null si el color no es parseable.
    /// </summary>
    private static string? Darken(string color, double factor)
    {
        if (color.Length != 7 || color[0] != '#') return null;
        if (!int.TryParse(color.AsSpan(1), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out int rgb))
        {
            return null;
        }

        int r = (int)(((rgb >> 16) & 0xFF) * factor);
        int g = (int)(((rgb >> 8) & 0xFF) * factor);
        int b = (int)((rgb & 0xFF) * factor);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
