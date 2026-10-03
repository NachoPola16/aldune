namespace Aldune.Core;

/// <summary>
/// Los temas de serie y las reglas para los propios. Los de serie se crean en cada acceso (listas
/// nuevas): quien los recibe puede modificarlos sin tocar el original.
///
/// Los colores de Sereno se calcularon en OKLCH con la misma claridad dentro de cada grupo
/// (oscuros L≈0.31, claros L≈0.925) y poco croma: se distinguen entre sí sin parecer
/// un arcoíris. Ver docs/superpowers/specs/2026-09-23-aldune-temas-design.md.
/// </summary>
public static class NoteThemes
{
    public const string ClassicId = "classic";
    public const string SereneId = "serene";
    /// <summary>Retirado en la 1.5 (lo sustituye "mismo color en todas las notas"). Se conserva el id
    /// para migrar a quien lo tenga activo (ver ThemeMigrations) y para que Resolve caiga en Clásico.</summary>
    public const string GraphiteId = "graphite";
    public const string XpId = "xp";
    public const string PastelId = "pastel";
    public const string AutumnId = "autumn";
    public const string OceanId = "ocean";

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
            // Renovado en la 1.5: los antiguos estaban a 0.020 (oscuros) y 0.008 (claros) de distancia
            // y se confundían. Misma claridad por grupo, croma justo para separarse y matices
            // repartidos por igual: oscuros L 0.31 C 0.052 cada 45°, claros L 0.925 C 0.042 cada 60°.
            DarkColors = ["#472525", "#422A11", "#36310E", "#1F371F", "#033936", "#113447", "#2B2D4A", "#3E273F"],
            LightColors = ["#FFDDD4", "#EFE7C7", "#D3EFD8", "#C7EFF4", "#D9E7FF", "#F5DDF6"],
        },
        // Los tres siguientes, con la misma regla (una claridad por grupo) y los matices repartidos
        // para que dos notas seguidas no se confundan (distancia OKLab >= 0.03, fijada en tests).
        new NoteTheme
        {
            Id = PastelId, Name = "Pastel", IsBuiltIn = true,
            // L 0.91, C 0.055. Solo claros: un pastel oscuro deja de serlo.
            // Rosa, Melocotón, Mantequilla, Menta, Cielo, Lavanda
            LightColors = ["#FFD3E6", "#FFD8C6", "#ECE2B9", "#C3EDD5", "#BDE9FF", "#E3DCFF"],
        },
        new NoteTheme
        {
            Id = AutumnId, Name = "Otoño", IsBuiltIn = true,
            // Tierra cálida, matices 5–130 cada 30°. Vino, Teja, Ocre, Mostaza, Oliva
            DarkColors = ["#512631", "#52281E", "#4A2F08", "#3D3605", "#2A3C15"],   // L 0.33
            LightColors = ["#FEC6D2", "#FFC9BB", "#F4D1A9", "#E1D9A8", "#C9E1B4"],  // L 0.88
        },
        new NoteTheme
        {
            Id = OceanId, Name = "Océano", IsBuiltIn = true,
            // Verdes y azules de mar, matices 140–280 cada 35°. Alga, Laguna, Turquesa, Cielo/Profundo, Marino
            DarkColors = ["#1B3914", "#003A2F", "#00373F", "#093351", "#2A2B53"],   // L 0.31
            LightColors = ["#C6EABE", "#ADEEDB", "#A7EBF8", "#C2E2FF", "#D7DBFF"],  // L 0.90
        },
        new NoteTheme
        {
            Id = XpId, Name = "XP", IsBuiltIn = true,
            // Los colores de nota de los aspectos XP: el amarillo de los avisos de Windows XP y tonos
            // de la misma familia. Solo claros, como Pastel. Los valores de partida (#FFFFE1 y
            // compañeros) tenían la claridad dispersa (0.93-0.99) y dos pares a 0.023 y 0.020; se
            // recalcularon en OKLCH con L 0.95 y C 0.03-0.044, matices 100/262/150/35/322/192
            // (distancia mínima 0.032). Amarillo, Azul, Verde, Melocotón, Lila, Turquesa.
            LightColors = ["#F7F1D0", "#E0F1FF", "#DCF9E1", "#FFE6DD", "#FFE7FF", "#CFFAF8"],
        },
    ];

    /// <summary>Colores de Sereno hasta la 1.4: los tienen guardados las notas que se crearon con él.</summary>
    public static IReadOnlyList<string> LegacySereneDarkColors { get; } =
        ["#2E3034", "#26323E", "#262F47", "#1D3538", "#283426", "#3C2D21", "#462527", "#392A3C"];

    public static IReadOnlyList<string> LegacySereneLightColors { get; } =
        ["#EBE6D9", "#EBE5E0", "#DEE8F0", "#DEEADE", "#F0E4D7", "#F2E2E1"];

    /// <summary>Color antiguo de Sereno → el nuevo del mismo puesto y tono. Claves en mayúsculas.</summary>
    public static IReadOnlyDictionary<string, string> SereneRecolorMap { get; } = BuildSereneRecolorMap();

    private static IReadOnlyDictionary<string, string> BuildSereneRecolorMap()
    {
        var serene = BuiltIn.First(theme => theme.Id == SereneId);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < LegacySereneDarkColors.Count; i++) map[LegacySereneDarkColors[i]] = serene.DarkColors[i];
        for (int i = 0; i < LegacySereneLightColors.Count; i++) map[LegacySereneLightColors[i]] = serene.LightColors[i];
        return map;
    }

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
