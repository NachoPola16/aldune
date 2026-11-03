namespace Aldune.Core.Importing;

/// <summary>Name or hex colour of another app to <see cref="ImportHue"/>; null for neutrals.</summary>
public static class ImportHues
{
    // OKLCH hues of the six colour families (the same ones the Classic note colours are built on).
    private static readonly (ImportHue Hue, double Degrees)[] Targets =
    [
        (ImportHue.Orange, 42), (ImportHue.Yellow, 92), (ImportHue.Green, 148),
        (ImportHue.Cyan, 205), (ImportHue.Blue, 268), (ImportHue.Pink, 330),
    ];

    /// <summary>Below this chroma a colour reads as grey and has no family worth matching.</summary>
    internal const double NeutralChroma = 0.03;

    // Names used by Google Keep (RED, ORANGE, YELLOW, GREEN, TEAL, BLUE, CERULEAN, PURPLE, PINK, BROWN, GRAY, DEFAULT) and Sticky Notes
    // (Yellow, Green, Pink, Purple, Blue, Gray, Charcoal). Purple has no family of its own: Blue is the nearest one in hue.
    private static readonly Dictionary<string, ImportHue> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["yellow"] = ImportHue.Yellow,
        ["green"] = ImportHue.Green,
        ["teal"] = ImportHue.Cyan,
        ["cerulean"] = ImportHue.Cyan,
        ["cyan"] = ImportHue.Cyan,
        ["blue"] = ImportHue.Blue,
        ["purple"] = ImportHue.Blue,
        ["pink"] = ImportHue.Pink,
        ["red"] = ImportHue.Orange,
        ["orange"] = ImportHue.Orange,
        ["brown"] = ImportHue.Orange,
    };

    public static ImportHue? FromName(string? name) =>
        name is not null && Names.TryGetValue(name.Trim(), out var hue) ? hue : null;

    public static ImportHue? FromHex(string? hex)
    {
        if (!OklchColor.TryFromHex(hex, out var color) || color.C < NeutralChroma) return null;
        return Targets.MinBy(t => HueDistance(t.Degrees, color.H)).Hue;
    }

    /// <summary>OKLCH hue of a family, for matching it against the colours of a theme.</summary>
    internal static double Degrees(ImportHue hue) => Targets.First(t => t.Hue == hue).Degrees;

    internal static double HueDistance(double a, double b)
    {
        var d = Math.Abs(a - b) % 360;
        return d > 180 ? 360 - d : d;
    }
}
