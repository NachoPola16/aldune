namespace Aldune.Core;

/// <summary>
/// Un color elegido por el usuario tiene que seguir dejando leer el texto que va encima o al lado (spec,
/// sección 2): se mueve su claridad OKLCH lo justo, conservando matiz y croma, que es lo que el usuario
/// está eligiendo. Una barra demasiado clara para texto blanco se oscurece hasta 4.5:1; un acento
/// demasiado oscuro bajo texto oscuro se aclara.
/// </summary>
public static class ColorFit
{
    /// <summary>Contraste WCAG entre dos #RRGGBB; 1 si alguno no es válido.</summary>
    public static double Ratio(string a, string b)
    {
        if (!NoteColorContrast.TryGetLuminance(a, out var la) || !NoteColorContrast.TryGetLuminance(b, out var lb)) return 1;
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary>Mueve el fondo hasta que <paramref name="text"/> se lea con al menos <paramref name="minimum"/>.</summary>
    public static string Background(string background, string text, double minimum) => Adjust(background, text, minimum);

    /// <summary>Mueve el texto hasta que se lea sobre <paramref name="background"/> con al menos <paramref name="minimum"/>.</summary>
    public static string Foreground(string foreground, string background, double minimum) => Adjust(foreground, background, minimum);

    public static string Lighter(string color, double amount) => Shift(color, amount);

    public static string Darker(string color, double amount) => Shift(color, -amount);

    private static string Shift(string color, double amount) =>
        OklchColor.TryFromHex(color, out var c) ? (c with { L = Math.Clamp(c.L + amount, 0, 1) }).ToHex() : color;

    private static string Adjust(string movable, string fixedColor, double minimum)
    {
        if (!OklchColor.TryFromHex(movable, out var color) || !NoteColorContrast.TryGetLuminance(fixedColor, out var fixedLuminance))
            return movable;
        if (Ratio(movable, fixedColor) >= minimum) return movable.ToUpperInvariant();

        // Contra un color claro se oscurece; contra uno oscuro se aclara. 0.18 es la luminancia en la
        // que el negro y el blanco dan el mismo contraste.
        bool darken = fixedLuminance > 0.18;
        double low = darken ? 0 : color.L, high = darken ? color.L : 1;
        string best = new OklchColor(darken ? 0 : 1, color.C, color.H).ToHex();
        // Búsqueda binaria de la claridad más cercana a la elegida que todavía llega al mínimo.
        for (int i = 0; i < 30; i++)
        {
            double middle = (low + high) / 2;
            var candidate = (color with { L = middle }).ToHex();
            bool readable = Ratio(candidate, fixedColor) >= minimum;
            if (darken)
            {
                if (readable) { best = candidate; low = middle; } else high = middle;
            }
            else
            {
                if (readable) { best = candidate; high = middle; } else low = middle;
            }
        }
        return best;
    }
}
