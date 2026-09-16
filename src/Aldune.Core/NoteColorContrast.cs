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
