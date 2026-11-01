namespace Aldune.Core;

/// <summary>
/// The red of destructive actions and errors. Its meaning must not change with the look, so every palette gets the same hue; what
/// changes is lightness and chroma, fitted in OKLCH to that palette's own backgrounds and accent so that it reads (text 4.5:1,
/// borders and icons 3:1) without turning into salmon or a fixed red that clashes with the look.
/// </summary>
internal static class DangerTokens
{
    public const double Hue = 27;

    private static readonly string[] TextBackgrounds = ["Ground", "Surface", "Raised", "Popup", "Hover"];
    private static readonly string[] BorderBackgrounds = ["Ground", "Surface", "Raised", "Popup"];

    public static IReadOnlyDictionary<string, string> Apply(IReadOnlyDictionary<string, string> source)
    {
        var p = new Dictionary<string, string>(source);
        if (!OklchColor.TryFromHex(p["Ground"], out var ground)) return source;
        bool light = ground.L > 0.6;
        double accentChroma = OklchColor.TryFromHex(p["Accent"], out var accent) ? accent.C : 0.1;
        // A more saturated look gets a more saturated red; a muted one a calmer red.
        double chroma = Math.Clamp(0.07 + 0.4 * accentChroma, 0.10, 0.15);

        string Red(double l, double c) => new OklchColor(l, c, Hue).ToHex();

        // Soft background of a warning strip or a danger row, with the normal text still readable on it.
        var bg = Red(Math.Clamp(ground.L + (light ? -0.07 : 0.08), 0.05, 0.97), light ? 0.04 : 0.06);
        bg = ColorFit.Background(bg, p["Text"], 4.5);
        bg = ColorFit.Background(bg, p["TextStrong"], 4.5);
        p["DangerBg"] = p["DangerHover"] = bg;

        // One red for text and icons, readable on every surface it can land on, danger strip included.
        var text = Red(light ? 0.48 : 0.76, chroma);
        foreach (var key in TextBackgrounds) text = ColorFit.Foreground(text, p[key], 4.5);
        text = ColorFit.Foreground(text, bg, 4.5);
        p["DangerText"] = p["ErrorText"] = text;
        p["OnDangerBg"] = ColorFit.Foreground(Red(light ? 0.36 : 0.88, 0.06), bg, 4.5);

        var border = Red(light ? 0.58 : 0.68, chroma);
        foreach (var key in BorderBackgrounds) border = ColorFit.Foreground(border, p[key], 3.0);
        p["ErrorBorder"] = border;

        // Filled destructive button: dark red under light text in dark looks, deep red under white in light ones.
        var onDanger = light ? "#FFFFFF" : Red(0.95, 0.015);
        var strong = ColorFit.Background(Red(light ? 0.52 : 0.40, light ? chroma + 0.03 : 0.10), onDanger, 4.5);
        var hover = ColorFit.Background(light ? ColorFit.Darker(strong, 0.04) : ColorFit.Lighter(strong, 0.05), onDanger, 4.5);
        p["DangerStrong"] = strong;
        p["DangerStrongHover"] = hover;
        p["OnDanger"] = onDanger;
        return p;
    }
}
