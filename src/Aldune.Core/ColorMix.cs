namespace Aldune.Core;

/// <summary>Mezcla lineal en sRGB entre dos colores #RRGGBB, la de las maquetas: los tintes de
/// <see cref="NoteFace"/> se afinaron así a ojo y en OKLCH no salen iguales.</summary>
public static class ColorMix
{
    /// <summary><paramref name="amount"/> 0 = <paramref name="from"/>, 1 = <paramref name="to"/>. Con un
    /// color inválido devuelve <paramref name="from"/> tal cual.</summary>
    public static string Toward(string from, string to, double amount)
    {
        if (!TryParse(from, out var a) || !TryParse(to, out var b)) return from;
        amount = Math.Clamp(amount, 0, 1);
        int Channel(int x, int y) => (int)Math.Round(x + (y - x) * amount, MidpointRounding.AwayFromZero);
        return $"#{Channel(a.R, b.R):X2}{Channel(a.G, b.G):X2}{Channel(a.B, b.B):X2}";
    }

    private static bool TryParse(string? hex, out (int R, int G, int B) rgb)
    {
        rgb = default;
        if (hex is null || hex.Length != 7 || hex[0] != '#') return false;
        if (!int.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture, out int value)) return false;
        rgb = ((value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF);
        return true;
    }
}
