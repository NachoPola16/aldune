namespace Aldune.Core;

/// <summary>Un color que el usuario puede cambiar en un aspecto, con muestras de un clic.</summary>
public sealed record AspectColorSlot(string Id, string Default, IReadOnlyList<string> Suggestions);

/// <summary>Varios huecos de una vez (las paletas de terminal de bash).</summary>
public sealed record AspectPreset(string Id, string Name, IReadOnlyDictionary<string, string> Colors);

/// <param name="Id">Clave estable en settings.json (<c>AspectColors</c>); no cambiar nunca.</param>
/// <param name="SuggestedThemeId">Tema de notas que el aspecto propone al elegirlo, o null.</param>
public sealed record AspectDefinition(
    AppearanceMode Mode, string Id, IReadOnlyList<AspectColorSlot> Slots, IReadOnlyList<AspectPreset> Presets,
    string? SuggestedThemeId);

/// <summary>
/// Los aspectos retro y sus colores personalizables (spec, sección 2). Valores por defecto y muestras
/// sacados de las maquetas. Los colores se guardan como <c>AspectColors: { "bash": { "accent": "#…" } }</c>;
/// lo que falta o no es un #RRGGBB válido cae en el valor por defecto.
/// </summary>
public static class AspectCatalog
{
    public static IReadOnlyList<AspectDefinition> Retro { get; } =
    [
        new(AppearanceMode.XpLight, "xp-light",
        [
            new("titleBar", "#0055E5", ["#0055E5", "#5A7A2E", "#8C8CA8", "#A8452A"]), // Luna azul, oliva, plata, teja
            new("accent", "#3C9A3C", ["#3C9A3C", "#0055E5", "#C46A1C"]),
        ], [], NoteThemes.XpId),
        new(AppearanceMode.XpDark, "xp-dark",
        [
            new("titleBar", "#1C4FA8", ["#1C4FA8", "#008080", "#6B2E8A", "#8A2E2E"]), // azul marino, verde azulado, morado, granate
            new("accent", "#4C86E8", ["#4C86E8", "#2EA8A8", "#C9A227"]),
        ], [], NoteThemes.SereneId),
        new(AppearanceMode.TelecomLight, "telecom-light",
        [
            new("ink", "#1C2A22", ["#1C2A22", "#1F2A44", "#3A2418"]),
            new("accent", "#2F7A3C", ["#2F7A3C", "#1F57B5", "#9C1F6E"]),
        ], [], null),
        new(AppearanceMode.TelecomDark, "telecom-dark",
        [
            new("panel", "#171C21", ["#171C21", "#10161C", "#1B1B1B"]),
            new("trace", "#F2D338", ["#F2D338", "#3FD0E0", "#7CFC7C"]),
        ], [], NoteThemes.SereneId),
        new(AppearanceMode.Bash, "bash",
        [
            new("background", "#282828", ["#282828", "#300A24", "#1E1E1E"]),
            new("user", "#B8BB26", ["#B8BB26", "#8AE234", "#FABD2F", "#8EC07C"]),
            new("path", "#83A598", ["#83A598", "#729FCF", "#D3869B", "#FE8019"]),
            new("accent", "#D79921", ["#D79921", "#458588", "#98971A", "#B16286"]),
        ],
        [
            // Gruvbox por defecto: cálido y apagado, nada de colores fuertes (decidido con el usuario).
            new("gruvbox", "Gruvbox", new Dictionary<string, string> { ["background"] = "#282828", ["user"] = "#B8BB26", ["path"] = "#83A598", ["accent"] = "#D79921" }),
            new("ubuntu", "Ubuntu", new Dictionary<string, string> { ["background"] = "#300A24", ["user"] = "#8AE234", ["path"] = "#729FCF", ["accent"] = "#E95420" }),
            new("tango", "Tango", new Dictionary<string, string> { ["background"] = "#1E1E1E", ["user"] = "#8AE234", ["path"] = "#729FCF", ["accent"] = "#3465A4" }),
        ], NoteThemes.SereneId),
        new(AppearanceMode.Phosphor, "phosphor",
        [
            new("phosphor", "#A8E6B4", ["#A8E6B4", "#FFB547", "#8EC9FF", "#E8E8E8"]), // verde, ámbar, azul, blanco
            new("details", "#D9A441", ["#D9A441", "#A8E6B4", "#E8E8E8"]),
        ], [], NoteThemes.SereneId),
    ];

    public static AspectDefinition? For(AppearanceMode mode) => Retro.FirstOrDefault(aspect => aspect.Mode == mode);

    /// <summary>Colores efectivos de un aspecto: los elegidos que sean válidos y, para el resto, los de
    /// fábrica. Vacío para los aspectos que no tienen huecos.</summary>
    public static IReadOnlyDictionary<string, string> Resolve(AppearanceMode mode, IReadOnlyDictionary<string, string>? chosen)
    {
        var result = new Dictionary<string, string>();
        if (For(mode) is not { } aspect) return result;
        foreach (var slot in aspect.Slots)
        {
            result[slot.Id] = chosen is not null && chosen.TryGetValue(slot.Id, out var value) && NoteDisplayColor.IsActive(value)
                ? value.ToUpperInvariant()
                : slot.Default;
        }
        return result;
    }
}
