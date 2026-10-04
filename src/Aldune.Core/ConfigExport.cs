using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aldune.Core;

/// <summary>Escribe el archivo de configuración (<c>*.aldune-config.json</c>). Solo salen los campos de
/// <see cref="ConfigFields.All"/> de las secciones marcadas.</summary>
public static class ConfigExport
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    public static string Build(AppSettings settings, ConfigSections sections, string appVersion)
    {
        var root = new JsonObject
        {
            ["format"] = ConfigFormat.Name,
            ["version"] = ConfigFormat.Version,
            ["app"] = appVersion,
        };
        AddSection(root, "appearance", ConfigSections.Appearance, settings, sections);
        AddSection(root, "settings", ConfigSections.Settings, settings, sections);
        return root.ToJsonString(Pretty);
    }

    private static void AddSection(JsonObject root, string name, ConfigSections section, AppSettings settings, ConfigSections wanted)
    {
        if (!wanted.HasFlag(section)) return;
        var node = new JsonObject();
        foreach (var field in ConfigFields.All.Where(f => f.Section == section))
            node[field.Id] = JsonSerializer.SerializeToNode(field.Get(settings));
        root[name] = node;
    }
}
