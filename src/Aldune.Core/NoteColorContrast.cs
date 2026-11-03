using System.Globalization;

namespace Aldune.Core;

/// <summary>WCAG contrast for opaque sRGB note colors, independent of the UI framework.</summary>
public static class NoteColorContrast
{
    public const string Ink = "#1E1A14";
    public const string White = "#FFFFFF";
    public const string Black = "#000000";
    public const double MinimumContrast = 4.5;

    /// <summary>Preserves warm ink where readable; black covers the small AA gap with white.</summary>
    public static string ForegroundFor(string? background)
    {
        if (!TryGetLuminance(background, out var luminance)) return Ink;
        TryGetLuminance(Ink, out var inkLuminance);
        if (Contrast(luminance, inkLuminance) >= MinimumContrast) return Ink;
        return Contrast(luminance, 1) >= MinimumContrast ? White : Black;
    }

    /// <summary>
    /// The ink for secondary text (dates, snippets) on a face: the ink softened towards the face as far as it can go (up to 30 %) while
    /// keeping 4.5:1. A plain opacity cannot promise that: 0.72 of an ink that already sits near 4.5:1 falls to about 3:1.
    /// </summary>
    public static string MutedInkFor(string? face, string ink)
    {
        if (!TryParse(face, out var f) || !TryParse(ink, out var i)) return ink;
        for (double fade = 0.3; fade > 0.01; fade -= 0.02)
        {
            string mixed = ToHex(Mix(i, f, fade));
            if (IsReadable(face, mixed)) return mixed;
        }
        return ink;
    }

    private static bool TryParse(string? color, out int rgb)
    {
        rgb = 0;
        return TryGetLuminance(color, out _)
            && int.TryParse(color.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb);
    }

    private static int Mix(int ink, int face, double fade)
    {
        int Part(int shift) => (int)Math.Round(((ink >> shift) & 0xFF) * (1 - fade) + ((face >> shift) & 0xFF) * fade);
        return (Part(16) << 16) | (Part(8) << 8) | Part(0);
    }

    private static string ToHex(int rgb) => "#" + rgb.ToString("X6", CultureInfo.InvariantCulture);

    public static bool IsReadable(string? background, string? foreground) =>
        TryGetLuminance(background, out var backgroundLuminance)
        && TryGetLuminance(foreground, out var foregroundLuminance)
        && Contrast(backgroundLuminance, foregroundLuminance) >= MinimumContrast;

    public static bool TryGetLuminance(string? color, out double luminance)
    {
        luminance = 0;
        if (color is null || color.Length != 7 || color[0] != '#') return false;
        // HexNumber permits whitespace, which is not part of a #RRGGBB color.
        foreach (char character in color.AsSpan(1))
        {
            if (!Uri.IsHexDigit(character)) return false;
        }
        if (!int.TryParse(color.AsSpan(1), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out int rgb)) return false;

        static double Channel(int value)
        {
            double channel = value / 255.0;
            return channel <= 0.04045
                ? channel / 12.92
                : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        luminance = 0.2126 * Channel((rgb >> 16) & 0xFF)
            + 0.7152 * Channel((rgb >> 8) & 0xFF)
            + 0.0722 * Channel(rgb & 0xFF);
        return true;
    }

    private static double Contrast(double first, double second) =>
        (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
}
