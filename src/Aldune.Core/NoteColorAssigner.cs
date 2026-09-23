namespace Aldune.Core;

/// <summary>
/// El color de una nota nueva: primero el tono (qué lista del tema) y después la regla (qué color
/// de esa lista). Mira los colores de las notas que ya hay en la vista del dock, en su orden; la
/// nueva aparece detrás de la última, así que "las vecinas" son las dos últimas.
///
/// Sustituye a <c>existing % Colors.Length</c>, que repetía el color de la vecina en cuanto se
/// borraba una nota: el número de notas no dice nada de qué colores tienen.
/// </summary>
public static class NoteColorAssigner
{
    private const int NeighborCount = 2;

    public static string Assign(NoteTheme theme, NoteTone tone, NoteColorAssignment rule,
        string? fixedColor, IReadOnlyList<string> dockColors)
    {
        var candidates = Candidates(theme, WantsDark(tone, dockColors));

        return rule switch
        {
            NoteColorAssignment.Fixed => FixedColor(theme, fixedColor) ?? candidates[0],
            NoteColorAssignment.Rotate => candidates[RotateIndex(candidates, dockColors)],
            NoteColorAssignment.MostDistinct => MostDistinct(candidates, dockColors),
            _ => AvoidNeighbors(candidates, dockColors),
        };
    }

    /// <summary>
    /// El color fijo, solo si es de este tema: se elige entre las pastillas del tema, y al cambiar de
    /// tema el de antes ya no está a la vista. Seguir usándolo crearía notas de un tema que el usuario
    /// acaba de dejar, sin forma de ver cuál es desde Ajustes.
    /// </summary>
    private static string? FixedColor(NoteTheme theme, string? fixedColor)
    {
        if (fixedColor is null) return null;
        int dark = IndexOf(theme.DarkColors, fixedColor);
        if (dark >= 0) return theme.DarkColors[dark];
        int light = IndexOf(theme.LightColors, fixedColor);
        return light >= 0 ? theme.LightColors[light] : null;
    }

    private static bool WantsDark(NoteTone tone, IReadOnlyList<string> dockColors) => tone switch
    {
        NoteTone.Dark => true,
        NoteTone.Light => false,
        // Alternando: oscura si no hay nada o si la última es clara.
        _ => dockColors.Count == 0 || !NoteColorDerivation.IsDark(dockColors[^1]),
    };

    /// <summary>La lista del tono pedido; si el tema no tiene de ese tono, la otra. Nunca vacía:
    /// un tema sin ningún color no pasa <see cref="NoteThemes.Sanitize"/>, y si aun así llegara se
    /// usa Clásico.</summary>
    private static IReadOnlyList<string> Candidates(NoteTheme theme, bool dark)
    {
        var preferred = dark ? theme.DarkColors : theme.LightColors;
        var other = dark ? theme.LightColors : theme.DarkColors;
        if (preferred.Count > 0) return preferred;
        if (other.Count > 0) return other;
        return NoteColorDerivation.ClassicColors;
    }

    /// <summary>El índice siguiente al último color de estos candidatos usado en el dock, o 0.</summary>
    private static int RotateIndex(IReadOnlyList<string> candidates, IReadOnlyList<string> dockColors)
    {
        for (int i = dockColors.Count - 1; i >= 0; i--)
        {
            int index = IndexOf(candidates, dockColors[i]);
            if (index >= 0) return (index + 1) % candidates.Count;
        }
        return 0;
    }

    private static string AvoidNeighbors(IReadOnlyList<string> candidates, IReadOnlyList<string> dockColors)
    {
        int start = RotateIndex(candidates, dockColors);
        var neighbors = dockColors.Skip(Math.Max(0, dockColors.Count - NeighborCount)).ToList();

        for (int step = 0; step < candidates.Count; step++)
        {
            var candidate = candidates[(start + step) % candidates.Count];
            if (IndexOf(neighbors, candidate) < 0) return candidate;
        }

        // Tema con tan pocos colores que todos están al lado: rotar sin más.
        return candidates[start];
    }

    private static string MostDistinct(IReadOnlyList<string> candidates, IReadOnlyList<string> dockColors)
    {
        var existing = dockColors
            .Select(color => OklchColor.TryFromHex(color, out var oklch) ? oklch : (OklchColor?)null)
            .OfType<OklchColor>()
            .ToList();
        if (existing.Count == 0) return candidates[0];

        string best = candidates[0];
        double bestDistance = double.MinValue;
        foreach (var candidate in candidates)
        {
            if (!OklchColor.TryFromHex(candidate, out var oklch)) continue;
            double nearest = existing.Min(other => oklch.DistanceTo(other));
            // Estrictamente mayor: en empate gana el primero en el orden del tema.
            if (nearest > bestDistance)
            {
                bestDistance = nearest;
                best = candidate;
            }
        }
        return best;
    }

    private static int IndexOf(IReadOnlyList<string> colors, string color)
    {
        for (int i = 0; i < colors.Count; i++)
        {
            if (string.Equals(colors[i], color, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }
}
