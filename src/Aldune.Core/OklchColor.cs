using System.Globalization;

namespace Aldune.Core;

/// <summary>
/// Un color en OKLCH: claridad percibida (L, 0-1), croma (C) y matiz en grados (H). Es el espacio
/// con el que se diseñaron las paletas de la app. En OKLCH, dos colores con la misma L pesan lo
/// mismo a la vista, cosa que en RGB o HSL no pasa. Conversión de Björn Ottosson (oklab).
/// </summary>
public readonly record struct OklchColor(double L, double C, double H)
{
    public static bool TryFromHex(string? hex, out OklchColor color)
    {
        color = default;
        if (hex is null || hex.Length != 7 || hex[0] != '#') return false;
        foreach (char character in hex.AsSpan(1))
        {
            if (!Uri.IsHexDigit(character)) return false;
        }

        int rgb = int.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        double r = ToLinear((rgb >> 16) & 0xFF);
        double g = ToLinear((rgb >> 8) & 0xFF);
        double b = ToLinear(rgb & 0xFF);

        double l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        double m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        double s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);

        double lightness = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s;
        double a = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
        double bb = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;

        double hue = Math.Atan2(bb, a) * 180 / Math.PI;
        color = new OklchColor(lightness, Math.Sqrt(a * a + bb * bb), hue < 0 ? hue + 360 : hue);
        return true;
    }

    /// <summary>
    /// A <c>#RRGGBB</c>. Si el color no cabe en sRGB se reduce el croma (búsqueda binaria) y se
    /// conservan claridad y matiz: recortar canal a canal cambiaría el matiz y la claridad, que es
    /// justo lo que las paletas mantienen fijo.
    /// </summary>
    public string ToHex()
    {
        var rgb = ToLinearRgb(L, C, H);
        if (!InGamut(rgb))
        {
            double low = 0, high = C;
            for (int i = 0; i < 30; i++)
            {
                double mid = (low + high) / 2;
                if (InGamut(ToLinearRgb(L, mid, H))) low = mid; else high = mid;
            }
            rgb = ToLinearRgb(L, low, H);
        }

        return $"#{ToByte(rgb.R):X2}{ToByte(rgb.G):X2}{ToByte(rgb.B):X2}";
    }

    /// <summary>Distancia euclídea en OKLab: la medida de "cuánto se parecen" dos colores.</summary>
    public double DistanceTo(OklchColor other)
    {
        var (a1, b1) = ToAb(this);
        var (a2, b2) = ToAb(other);
        double dl = L - other.L, da = a1 - a2, db = b1 - b2;
        return Math.Sqrt(dl * dl + da * da + db * db);
    }

    private static (double A, double B) ToAb(OklchColor color)
    {
        double radians = color.H * Math.PI / 180;
        return (color.C * Math.Cos(radians), color.C * Math.Sin(radians));
    }

    private static (double R, double G, double B) ToLinearRgb(double lightness, double chroma, double hue)
    {
        var (a, b) = ToAb(new OklchColor(lightness, chroma, hue));
        double l = Math.Pow(lightness + 0.3963377774 * a + 0.2158037573 * b, 3);
        double m = Math.Pow(lightness - 0.1055613458 * a - 0.0638541728 * b, 3);
        double s = Math.Pow(lightness - 0.0894841775 * a - 1.2914855480 * b, 3);
        return (
            4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
            -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s);
    }

    private const double GamutTolerance = 1e-4;

    private static bool InGamut((double R, double G, double B) rgb) =>
        rgb.R >= -GamutTolerance && rgb.R <= 1 + GamutTolerance
        && rgb.G >= -GamutTolerance && rgb.G <= 1 + GamutTolerance
        && rgb.B >= -GamutTolerance && rgb.B <= 1 + GamutTolerance;

    private static double ToLinear(int channel)
    {
        double value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static int ToByte(double linear)
    {
        linear = Math.Clamp(linear, 0, 1);
        double encoded = linear <= 0.0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;
        return (int)Math.Round(Math.Clamp(encoded, 0, 1) * 255);
    }
}
