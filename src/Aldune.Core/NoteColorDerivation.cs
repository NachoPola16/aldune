namespace Aldune.Core;

/// <summary>
/// El borde y el color de la etiqueta de una nota, derivados de su cara en OKLCH. Con temas,
/// cualquier color tiene que salir bien sin escribir sus acompañantes a mano:
///
/// - Cara clara: borde a L−0.16 y etiqueta a L−0.44, mismo matiz y croma. Es la regla con la que
///   se diseñó la paleta de fábrica.
/// - Cara oscura: borde a L+0.07 (el "filo superior" de la pestaña) y etiqueta a L 0.86, con un
///   poco más de croma para que no se vea gris.
///
/// Los seis colores de fábrica van por una tabla aparte: se afinaron a mano y la fórmula no los
/// reproduce al píxel. Si una etiqueta calculada no llegara a 4,5:1, se usa la tinta adaptativa.
/// </summary>
public static class NoteColorDerivation
{
    public const double DarkThreshold = 0.6;

    private static readonly (string Face, string Rim, string Label)[] Classic =
    {
        ("#EBD38B", "#B7A059", "#614E00"), // citron  oklch(0.87 0.095 92)
        ("#AAE6B1", "#78B280", "#006123"), // sage    oklch(0.87 0.095 148)
        ("#83E7F2", "#4CB3BE", "#005B63"), // sky     oklch(0.87 0.095 205)
        ("#C2D4FF", "#90A1CA", "#3A4C83"), // iris    oklch(0.87 0.063 268)
        ("#F9BEF2", "#C38CBE", "#743170"), // rose    oklch(0.87 0.095 330)
        ("#FFC5AF", "#C9937E", "#7D391D"), // amber   oklch(0.87 0.073 42)
    };

    public static IReadOnlyList<string> ClassicColors { get; } = Classic.Select(entry => entry.Face).ToArray();

    public static bool IsDark(string? color) =>
        OklchColor.TryFromHex(color, out var oklch) && oklch.L < DarkThreshold;

    public static string RimFor(string color)
    {
        if (FindClassic(color) is { } classic) return classic.Rim;
        if (!OklchColor.TryFromHex(color, out var face)) return color;

        return face.L < DarkThreshold
            ? (face with { L = Math.Min(face.L + 0.07, 1) }).ToHex()
            : (face with { L = Math.Max(face.L - 0.16, 0) }).ToHex();
    }

    public static string LabelFor(string color)
    {
        if (FindClassic(color) is { } classic) return classic.Label;
        if (!OklchColor.TryFromHex(color, out var face)) return NoteColorContrast.ForegroundFor(color);

        var label = face.L < DarkThreshold
            ? new OklchColor(0.86, Math.Min(face.C * 1.6, 0.08), face.H).ToHex()
            : (face with { L = Math.Max(face.L - 0.44, 0) }).ToHex();

        return NoteColorContrast.IsReadable(color, label) ? label : NoteColorContrast.ForegroundFor(color);
    }

    /// <summary>
    /// Contorno del guion de una nota oscura en la tira de reposo del dock, o null si la cara ya se
    /// ve sola. La tira es del mismo oscuro que el chrome (L≈0.27) y las caras oscuras de los temas
    /// rondan L 0.30: sin contorno el guion desaparecía. El borde normal (L+0.07) no basta a ese
    /// tamaño, así que aquí se sube más la claridad, con el mismo matiz y un poco más de croma: la
    /// cara sigue siendo el color exacto de la nota y el contorno se lee como parte de ella.
    /// </summary>
    public static string? RestOutlineFor(string? color)
    {
        if (!OklchColor.TryFromHex(color, out var face) || face.L >= DarkThreshold) return null;

        return new OklchColor(Math.Max(face.L + 0.2, 0.47), Math.Min(face.C * 1.3, 0.08), face.H).ToHex();
    }

    private static (string Face, string Rim, string Label)? FindClassic(string? color)
    {
        foreach (var entry in Classic)
        {
            if (string.Equals(entry.Face, color, StringComparison.OrdinalIgnoreCase)) return entry;
        }
        return null;
    }
}
