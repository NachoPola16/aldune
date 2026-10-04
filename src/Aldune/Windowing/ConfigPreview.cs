using Aldune.Core;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>Una fila de la vista previa de importar: nombre ya traducido y valores ya formateados.</summary>
internal sealed record ConfigPreviewRow(string Name, string Old, string New);

/// <summary>
/// Pasa el plan de Core (valores en bruto: números para los atajos, true/false, nombres de enum en
/// inglés) a lo que se enseña en la vista previa. Solo presenta: no cambia los datos del plan.
/// </summary>
internal static class ConfigPreview
{
    public static IReadOnlyList<ConfigPreviewRow> Rows(ConfigImportPlan plan, AppSettings current)
    {
        var rows = new List<ConfigPreviewRow>();
        bool hotkeyDone = false, recentDone = false;

        foreach (var change in plan.Changes)
        {
            switch (change.FieldId)
            {
                // Modificadores y tecla son dos campos en el archivo pero una sola cosa para quien lo lee:
                // una fila con la combinación de antes y la de ahora, como la enseña Ajustes. Se emite al
                // encontrar el primero de los dos.
                case "hotkeyModifiers" or "hotkeyKey":
                    if (hotkeyDone) break;
                    hotkeyDone = true;
                    AddCombination(rows, plan, Strings.ConfigFieldHotkeyCombo, "hotkeyModifiers", "hotkeyKey",
                        current.HotkeyModifiers, current.HotkeyKey, current.Hotkey, HotkeyBinding.Default);
                    break;
                case "recentHotkeyModifiers" or "recentHotkeyKey":
                    if (recentDone) break;
                    recentDone = true;
                    AddCombination(rows, plan, Strings.ConfigFieldRecentHotkeyCombo, "recentHotkeyModifiers", "recentHotkeyKey",
                        current.RecentNoteHotkeyModifiers, current.RecentNoteHotkeyKey, current.RecentNoteHotkey,
                        HotkeyBinding.RecentNoteDefault);
                    break;
                default:
                    rows.Add(new(Strings.ConfigFieldName(change.FieldId),
                        FormatValue(change.FieldId, change.OldValue), FormatValue(change.FieldId, change.NewValue)));
                    break;
            }
        }
        return rows;
    }

    // Lo que no cambia en el archivo se queda como está hoy; un nulo vale la combinación de fábrica (lo
    // mismo que resuelven AppSettings.Hotkey y RecentNoteHotkey). Si las dos combinaciones se ven igual
    // (p. ej. el archivo trae explícito lo que ya era el valor de fábrica) no hay nada que enseñar.
    private static void AddCombination(List<ConfigPreviewRow> rows, ConfigImportPlan plan, string name,
        string modifiersId, string keyId, uint? currentModifiers, uint? currentKey, HotkeyBinding before, HotkeyBinding factory)
    {
        var modifiers = ValueOf(plan, modifiersId, currentModifiers);
        var key = ValueOf(plan, keyId, currentKey);
        var after = modifiers is { } m && key is { } k && new HotkeyBinding(m, k).IsValid ? new HotkeyBinding(m, k) : factory;
        if (before.DisplayName != after.DisplayName) rows.Add(new(name, before.DisplayName, after.DisplayName));
    }

    private static uint? ValueOf(ConfigImportPlan plan, string id, uint? current)
    {
        var change = plan.Changes.FirstOrDefault(c => c.FieldId == id);
        if (change is null) return current;
        return uint.TryParse(change.NewValue, out var value) ? value : null;   // «—» = nulo
    }

    /// <summary>Valor en el idioma de la interfaz: true/false como Activado/Desactivado y los enums con la
    /// etiqueta que ya usa Ajustes. Lo que no tiene etiqueta traducida (idioma, id del tema, listas de colores
    /// y temas propios, «—») se deja como lo da Core.</summary>
    public static string FormatValue(string fieldId, string raw)
    {
        if (raw == "true") return Strings.ConfigValueOn;
        if (raw == "false") return Strings.ConfigValueOff;

        return fieldId switch
        {
            "appearance" when Enum.TryParse(raw, out AppearanceMode mode) => AppearanceName(mode) ?? raw,
            "dockEdge" when Enum.TryParse(raw, out EdgePosition edge) => edge switch
            {
                EdgePosition.Right => Strings.EdgeRight,
                EdgePosition.Left => Strings.EdgeLeft,
                EdgePosition.Top => Strings.EdgeTop,
                EdgePosition.Bottom => Strings.EdgeBottom,
                _ => raw,
            },
            "dockView" when Enum.TryParse(raw, out DockViewKind view) => view switch
            {
                DockViewKind.Active => Strings.DockViewActive,
                DockViewKind.Archived => Strings.DockViewArchived,
                DockViewKind.Trashed => Strings.DockViewTrash,
                DockViewKind.Tag => Strings.DockViewTags,
                _ => raw,
            },
            "newNoteTone" when Enum.TryParse(raw, out NoteTone tone) => tone switch
            {
                NoteTone.Light => Strings.ToneLight,
                NoteTone.Dark => Strings.ToneDark,
                NoteTone.Both => Strings.ToneBoth,
                _ => raw,
            },
            "colorAssignment" when Enum.TryParse(raw, out NoteColorAssignment rule) => rule switch
            {
                NoteColorAssignment.RotateAvoidNeighbors => Strings.AssignAvoidNeighbors,
                NoteColorAssignment.Rotate => Strings.AssignRotate,
                NoteColorAssignment.MostDistinct => Strings.AssignMostDistinct,
                NoteColorAssignment.Fixed => Strings.AssignFixed,
                _ => raw,
            },
            "autoHideDelayUnit" when Enum.TryParse(raw, out TaskDelayUnit unit) => unit switch
            {
                TaskDelayUnit.Minutes => Strings.TaskDelayMinutesUnit,
                TaskDelayUnit.Hours => Strings.TaskDelayHoursUnit,
                TaskDelayUnit.Days => Strings.TaskDelayDaysUnit,
                TaskDelayUnit.Weeks => Strings.TaskDelayWeeksUnit,
                _ => raw,
            },
            _ => raw,
        };
    }

    private static string? AppearanceName(AppearanceMode mode) => mode switch
    {
        AppearanceMode.Dark => Strings.AppearanceDark,
        AppearanceMode.Light => Strings.AppearanceLight,
        AppearanceMode.Pastel => Strings.AppearancePastel,
        AppearanceMode.Midnight => Strings.AppearanceMidnight,
        AppearanceMode.System => Strings.AppearanceSystem,
        AppearanceMode.XpLight => Strings.AppearanceXpLight,
        AppearanceMode.XpDark => Strings.AppearanceXpDark,
        AppearanceMode.TelecomLight => Strings.AppearanceTelecomLight,
        AppearanceMode.TelecomDark => Strings.AppearanceTelecomDark,
        AppearanceMode.Bash => Strings.AppearanceBash,
        AppearanceMode.Phosphor => Strings.AppearancePhosphor,
        _ => null,
    };
}
