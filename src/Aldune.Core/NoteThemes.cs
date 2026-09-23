namespace Aldune.Core;

/// <summary>
/// Los temas de serie y las reglas para los propios. Los de serie se crean en cada acceso (listas
/// nuevas): quien los recibe puede modificarlos sin tocar el original.
///
/// Los colores de Sereno y Grafito se calcularon en OKLCH con la misma claridad dentro de cada grupo
/// (oscuros L≈0.31 y 0.30, claros L≈0.925 y 0.93) y poco croma: se distinguen entre sí sin parecer
/// un arcoíris. Ver docs/superpowers/specs/2026-09-23-aldune-temas-design.md.
/// </summary>
public static class NoteThemes
{
    public const string ClassicId = "classic";
    public const string SereneId = "serene";
    public const string GraphiteId = "graphite";

    public static IReadOnlyList<NoteTheme> BuiltIn =>
    [
        new NoteTheme
        {
            Id = ClassicId, Name = "Clásico", IsBuiltIn = true,
            LightColors = NoteColorDerivation.ClassicColors.ToList(),
        },
        new NoteTheme
        {
            Id = SereneId, Name = "Sereno", IsBuiltIn = true,
            // Grafito, Pizarra, Tinta, Petróleo, Musgo, Tabaco, Burdeos, Ciruela
            DarkColors = ["#2E3034", "#26323E", "#262F47", "#1D3538", "#283426", "#3C2D21", "#462527", "#392A3C"],
            // Hueso, Piedra, Niebla, Salvia, Lino, Polvo
            LightColors = ["#EBE6D9", "#EBE5E0", "#DEE8F0", "#DEEADE", "#F0E4D7", "#F2E2E1"],
        },
        new NoteTheme
        {
            Id = GraphiteId, Name = "Grafito", IsBuiltIn = true,
            // Carbón, Grafito, Humo, Acero, Ónice
            DarkColors = ["#2F2D2C", "#2B2E33", "#332C29", "#262F36", "#2E2E2E"],
            // Papel, Tiza, Arena, Ceniza
            LightColors = ["#EBE7E0", "#E4E8ED", "#EFE6DD", "#E8E8E8"],
        },
    ];

    public static IReadOnlyList<NoteTheme> All(IEnumerable<NoteTheme>? custom) =>
        BuiltIn.Concat(custom ?? Enumerable.Empty<NoteTheme>()).ToList();

    /// <summary>El tema con ese id, o Clásico si no existe (borrado, o settings.json editado a mano).</summary>
    public static NoteTheme Resolve(string? id, IEnumerable<NoteTheme>? custom) =>
        All(custom).FirstOrDefault(theme => theme.Id == id) ?? BuiltIn[0];

    /// <summary>
    /// Limpia los temas propios leídos de disco: quita los colores que no son <c>#RRGGBB</c> y los
    /// normaliza a mayúsculas, y descarta los temas sin ningún color, los que usan el id de uno de
    /// serie y los que repiten un id ya visto. Nunca marca uno como de serie.
    /// </summary>
    public static List<NoteTheme> Sanitize(IEnumerable<NoteTheme>? custom)
    {
        var builtInIds = BuiltIn.Select(theme => theme.Id).ToHashSet();
        var seen = new HashSet<string>();
        var clean = new List<NoteTheme>();

        foreach (var theme in custom ?? Enumerable.Empty<NoteTheme>())
        {
            if (theme is null || string.IsNullOrWhiteSpace(theme.Id)) continue;
            if (builtInIds.Contains(theme.Id) || !seen.Add(theme.Id)) continue;

            var dark = CleanColors(theme.DarkColors);
            var light = CleanColors(theme.LightColors);
            if (dark.Count == 0 && light.Count == 0) continue;

            clean.Add(new NoteTheme
            {
                Id = theme.Id,
                Name = string.IsNullOrWhiteSpace(theme.Name) ? theme.Id : theme.Name.Trim(),
                DarkColors = dark,
                LightColors = light,
            });
        }

        return clean;
    }

    public static NoteTheme Duplicate(NoteTheme source, string name) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = name,
        DarkColors = source.DarkColors.ToList(),
        LightColors = source.LightColors.ToList(),
    };

    private static List<string> CleanColors(IEnumerable<string>? colors) =>
        (colors ?? Enumerable.Empty<string>())
            .Where(color => OklchColor.TryFromHex(color, out _))
            .Select(color => color.ToUpperInvariant())
            .ToList();
}
