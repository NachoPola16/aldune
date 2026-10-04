using System.Text.Json;

namespace Aldune.Core;

[Flags]
public enum ConfigSections
{
    None = 0,
    Appearance = 1,
    Settings = 2,
    All = Appearance | Settings,
}

public static class ConfigFormat
{
    public const string Name = "aldune-config";
    public const int Version = 1;
}

/// <summary>
/// Un ajuste que puede viajar en el archivo de configuración. La lista de <see cref="ConfigFields.All"/>
/// es lo único que sale al exportar y lo único que se toca al importar: lo que no está aquí (sincronización,
/// claves, pantalla elegida…) no puede salir ni entrar por esta vía. El id es la clave del JSON y no se
/// cambia nunca.
/// </summary>
public sealed record ConfigField(
    string Id,
    ConfigSections Section,
    Func<AppSettings, object?> Get,
    Func<JsonElement, (bool Ok, object? Value)> Read,
    Action<AppSettings, object?> Set,
    Func<object?, string> Show);

public static class ConfigFields
{
    // Estos dos van antes de All: los inicializadores estáticos corren en orden de texto y Build() los
    // usa; si estuvieran debajo, Defaults sería null al construir los campos.
    private static readonly AppSettings Defaults = new();

    private static readonly (bool, object?) Invalid = (false, null);

    public static IReadOnlyList<ConfigField> All { get; } = Build();

    public static ConfigField? Find(string id) => All.FirstOrDefault(field => field.Id == id);

    private static List<ConfigField> Build() =>
    [
        // — Aspecto —
        Enum<AppearanceMode>("appearance", ConfigSections.Appearance, s => s.Appearance, (s, v) => s.Appearance = v),
        Custom("aspectColors", ConfigSections.Appearance, s => s.AspectColors, ReadAspectColors, (s, v) => s.AspectColors = (Dictionary<string, Dictionary<string, string>>?)v, ShowAspectColors),
        NullableBool("squareCorners", ConfigSections.Appearance, s => s.SquareCorners, (s, v) => s.SquareCorners = v),
        NullableBool("syncSignal", ConfigSections.Appearance, s => s.ShowSyncSignal, (s, v) => s.ShowSyncSignal = v),
        Color("uniformNoteColor", ConfigSections.Appearance, s => s.UniformNoteColor, (s, v) => s.UniformNoteColor = v),
        Text("noteTheme", ConfigSections.Appearance, s => s.ActiveThemeId, (s, v) => s.ActiveThemeId = v, valid: _ => true),
        Custom("customThemes", ConfigSections.Appearance, s => s.CustomThemes, ReadThemes, (s, v) => s.CustomThemes = (List<NoteTheme>)v!, ShowThemes),
        Enum<NoteTone>("newNoteTone", ConfigSections.Appearance, s => s.NewNoteTone, (s, v) => s.NewNoteTone = v),
        Enum<NoteColorAssignment>("colorAssignment", ConfigSections.Appearance, s => s.ColorAssignment, (s, v) => s.ColorAssignment = v),
        Color("fixedNoteColor", ConfigSections.Appearance, s => s.FixedNoteColor, (s, v) => s.FixedNoteColor = v),

        // — Ajustes —
        Text("language", ConfigSections.Settings, s => s.Language, (s, v) => s.Language = v, valid: code => UiLanguages.All.Any(language => language.Code == code)),
        Bool("simplifiedMode", ConfigSections.Settings, s => s.SimplifiedMode, (s, v) => s.SimplifiedMode = v),
        Bool("hotkeyEnabled", ConfigSections.Settings, s => s.GlobalHotkeyEnabled, (s, v) => s.GlobalHotkeyEnabled = v),
        NullableUInt("hotkeyModifiers", ConfigSections.Settings, s => s.HotkeyModifiers, (s, v) => s.HotkeyModifiers = v),
        NullableUInt("hotkeyKey", ConfigSections.Settings, s => s.HotkeyKey, (s, v) => s.HotkeyKey = v),
        Bool("recentHotkeyEnabled", ConfigSections.Settings, s => s.RecentNoteHotkeyEnabled, (s, v) => s.RecentNoteHotkeyEnabled = v),
        NullableUInt("recentHotkeyModifiers", ConfigSections.Settings, s => s.RecentNoteHotkeyModifiers, (s, v) => s.RecentNoteHotkeyModifiers = v),
        NullableUInt("recentHotkeyKey", ConfigSections.Settings, s => s.RecentNoteHotkeyKey, (s, v) => s.RecentNoteHotkeyKey = v),
        Enum<EdgePosition>("dockEdge", ConfigSections.Settings, s => s.DockEdge, (s, v) => s.DockEdge = v),
        Enum<DockViewKind>("dockView", ConfigSections.Settings, s => s.DockView, (s, v) => s.DockView = v),
        Bool("keepDockOpen", ConfigSections.Settings, s => s.KeepDockOpen, (s, v) => s.KeepDockOpen = v),
        Bool("showNotePreview", ConfigSections.Settings, s => s.ShowNotePreview, (s, v) => s.ShowNotePreview = v),
        Bool("hideOnFullscreen", ConfigSections.Settings, s => s.HideOnFullscreen, (s, v) => s.HideOnFullscreen = v),
        Bool("trackpadGestures", ConfigSections.Settings, s => s.TrackpadGestures, (s, v) => s.TrackpadGestures = v),
        Bool("moveCompletedTasksToEnd", ConfigSections.Settings, s => s.MoveCompletedTasksToEnd, (s, v) => s.MoveCompletedTasksToEnd = v),
        Bool("autoHideCompletedTasks", ConfigSections.Settings, s => s.AutoHideCompletedTasks, (s, v) => s.AutoHideCompletedTasks = v),
        Int("autoHideDelayValue", ConfigSections.Settings, 1, 999, s => s.AutoHideCompletedTasksDelayValue, (s, v) => s.AutoHideCompletedTasksDelayValue = v),
        Enum<TaskDelayUnit>("autoHideDelayUnit", ConfigSections.Settings, s => s.AutoHideCompletedTasksDelayUnit, (s, v) => s.AutoHideCompletedTasksDelayUnit = v),
        Int("trashRetentionDays", ConfigSections.Settings, 1, 3650, s => s.TrashRetentionDays, (s, v) => s.TrashRetentionDays = v),
        Bool("rememberNotePositions", ConfigSections.Settings, s => s.RememberNotePositions, (s, v) => s.RememberNotePositions = v),
        Bool("checkForUpdates", ConfigSections.Settings, s => s.CheckForUpdatesAutomatically, (s, v) => s.CheckForUpdatesAutomatically = v),
    ];

    private static ConfigField Custom(string id, ConfigSections section, Func<AppSettings, object?> get,
        Func<JsonElement, (bool, object?)> read, Action<AppSettings, object?> set, Func<object?, string> show) =>
        new(id, section, get, read, set, show);

    private static ConfigField Bool(string id, ConfigSections section, Func<AppSettings, bool> get, Action<AppSettings, bool> set) =>
        new(id, section, s => get(s),
            e => e.ValueKind is JsonValueKind.True or JsonValueKind.False ? (true, e.GetBoolean()) : Invalid,
            (s, v) => set(s, (bool)v!), v => v is true ? "true" : "false");

    private static ConfigField NullableBool(string id, ConfigSections section, Func<AppSettings, bool?> get, Action<AppSettings, bool?> set) =>
        new(id, section, s => get(s),
            e => e.ValueKind switch
            {
                JsonValueKind.True => (true, true),
                JsonValueKind.False => (true, false),
                JsonValueKind.Null => (true, null),
                _ => Invalid,
            },
            (s, v) => set(s, (bool?)v), v => v is null ? "—" : v is true ? "true" : "false");

    private static ConfigField Int(string id, ConfigSections section, int min, int max, Func<AppSettings, int> get, Action<AppSettings, int> set)
    {
        var fallback = get(Defaults);
        return new(id, section, s => get(s),
            e => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n)
                ? (true, n >= min && n <= max ? n : fallback)   // fuera de rango: el valor por defecto
                : Invalid,
            (s, v) => set(s, (int)v!), v => v?.ToString() ?? "—");
    }

    private static ConfigField NullableUInt(string id, ConfigSections section, Func<AppSettings, uint?> get, Action<AppSettings, uint?> set) =>
        new(id, section, s => get(s),
            e => e.ValueKind switch
            {
                JsonValueKind.Null => (true, null),
                JsonValueKind.Number when e.TryGetUInt32(out var n) => (true, (uint?)n),
                _ => Invalid,
            },
            (s, v) => set(s, (uint?)v), v => v?.ToString() ?? "—");

    // Get devuelve el propio enum (no su número) para que antes y después se muestren igual.
    private static ConfigField Enum<T>(string id, ConfigSections section, Func<AppSettings, T> get, Action<AppSettings, T> set) where T : struct, System.Enum
    {
        var fallback = get(Defaults);
        return new(id, section, s => get(s),
            e => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n)
                ? (true, System.Enum.IsDefined(typeof(T), n) ? (T)System.Enum.ToObject(typeof(T), n) : fallback)
                : Invalid,
            (s, v) => set(s, (T)v!), v => v is T t ? t.ToString() : v?.ToString() ?? "—");
    }

    private static ConfigField Color(string id, ConfigSections section, Func<AppSettings, string?> get, Action<AppSettings, string?> set) =>
        new(id, section, s => get(s),
            e => e.ValueKind switch
            {
                JsonValueKind.Null => (true, null),
                JsonValueKind.String when NoteDisplayColor.IsActive(e.GetString()) => (true, e.GetString()!.ToUpperInvariant()),
                _ => Invalid,   // un color inválido se descarta, no cambia nada
            },
            (s, v) => set(s, (string?)v), v => v as string ?? "—");

    private static ConfigField Text(string id, ConfigSections section, Func<AppSettings, string?> get, Action<AppSettings, string?> set, Func<string, bool> valid) =>
        new(id, section, s => get(s),
            e => e.ValueKind switch
            {
                JsonValueKind.Null => (true, null),
                JsonValueKind.String when !string.IsNullOrWhiteSpace(e.GetString()) && valid(e.GetString()!) => (true, e.GetString()),
                _ => Invalid,
            },
            (s, v) => set(s, (string?)v), v => v as string ?? "—");

    // Colores por aspecto: solo aspectos y huecos que existen, solo #RRGGBB válidos.
    private static (bool, object?) ReadAspectColors(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null) return (true, null);
        if (element.ValueKind != JsonValueKind.Object) return Invalid;

        var result = new Dictionary<string, Dictionary<string, string>>();
        foreach (var aspect in element.EnumerateObject())
        {
            var definition = AspectCatalog.Retro.FirstOrDefault(a => a.Id == aspect.Name);
            if (definition is null || aspect.Value.ValueKind != JsonValueKind.Object) continue;
            var colors = new Dictionary<string, string>();
            foreach (var slot in aspect.Value.EnumerateObject())
            {
                if (definition.Slots.All(s => s.Id != slot.Name)) continue;
                if (slot.Value.ValueKind == JsonValueKind.String && NoteDisplayColor.IsActive(slot.Value.GetString()))
                    colors[slot.Name] = slot.Value.GetString()!.ToUpperInvariant();
            }
            if (colors.Count > 0) result[aspect.Name] = colors;
        }
        return (true, result.Count == 0 ? null : result);
    }

    private static string ShowAspectColors(object? value) =>
        value is Dictionary<string, Dictionary<string, string>> all && all.Count > 0
            ? string.Join("; ", all.OrderBy(a => a.Key).Select(a => $"{a.Key}: " + string.Join(", ", a.Value.OrderBy(c => c.Key).Select(c => $"{c.Key}={c.Value}"))))
            : "—";

    // Temas propios: los mismos del archivo pasados por NoteThemes.Sanitize (colores válidos, ids únicos).
    private static (bool, object?) ReadThemes(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array) return Invalid;
        try
        {
            var themes = JsonSerializer.Deserialize<List<NoteTheme>>(element.GetRawText());
            return (true, NoteThemes.Sanitize(themes));
        }
        catch (JsonException)
        {
            return Invalid;
        }
    }

    private static string ShowThemes(object? value) =>
        value is List<NoteTheme> themes && themes.Count > 0
            ? string.Join(", ", themes.Select(t => $"{t.Name} ({t.DarkColors.Count + t.LightColors.Count})"))
            : "—";
}
