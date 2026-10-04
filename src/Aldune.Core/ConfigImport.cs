using System.Text.Json;

namespace Aldune.Core;

public sealed record ConfigChange(string FieldId, ConfigSections Section, string OldValue, string NewValue);

/// <summary>Lo que importar un archivo cambiaría, y cómo aplicarlo. Crear el plan no toca nada.</summary>
public sealed class ConfigImportPlan
{
    private readonly List<(ConfigField Field, object? Value)> _apply = [];
    private readonly List<ConfigChange> _changes = [];

    public bool IsValid { get; init; }
    public string? Error { get; init; }
    public int FileVersion { get; init; }
    public string? FileApp { get; init; }
    public ConfigSections SectionsInFile { get; init; }
    public IReadOnlyList<ConfigChange> Changes => _changes;

    /// <summary>El idioma solo se aplica al reiniciar (como en Ajustes): la vista previa lo avisa.</summary>
    public bool ChangesLanguage => _changes.Any(c => c.FieldId == "language");

    internal void Add(ConfigField field, object? value, ConfigChange change)
    {
        _apply.Add((field, value));
        _changes.Add(change);
    }

    /// <summary>Aplica los cambios del plan. Con un plan inválido o sin cambios no hace nada.</summary>
    public void ApplyTo(AppSettings target)
    {
        foreach (var (field, value) in _apply) field.Set(target, value);
    }
}

/// <summary>
/// Lee un archivo de configuración con tolerancia (spec, sección 6): un campo o una sección que falta no
/// se toca; los campos desconocidos se ignoran; un color inválido o un tipo equivocado descartan ese
/// campo; un enum fuera de rango cae en su valor por defecto; una versión futura se lee con lo que se
/// entienda. Solo un archivo roto o que no es de Aldune es un error.
/// </summary>
public static class ConfigImport
{
    public static ConfigImportPlan Plan(string json, AppSettings current)
    {
        if (string.IsNullOrWhiteSpace(json)) return Fail("El archivo está vacío.");

        JsonDocument document;
        try
        {
            // Un BOM delante (ReadAllText lo quita, pero un texto leído de otra forma lo conserva) no es un error.
            document = JsonDocument.Parse(json.TrimStart((char)0xFEFF));
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return Fail("El archivo no es un JSON válido.");
        }

        using (document)
        {
            // Parse no lee los textos, solo los acepta: un escape de sustituto suelto ("\ud800") salta después, al leer
            // el valor (GetString), como InvalidOperationException. Un archivo así es corrupto o hecho a propósito:
            // error, no excepción. El plan se arma al final, así que no queda nada a medias.
            try
            {
                return Read(document.RootElement, current);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException or JsonException)
            {
                return Fail("El archivo no es un JSON válido.");
            }
        }
    }

    private static ConfigImportPlan Read(JsonElement root, AppSettings current)
    {
        // El ValueKind se mira antes de GetString: con "format": 5 GetString lanzaría en vez de decir "no es de Aldune".
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("format", out var format) ||
            format.ValueKind != JsonValueKind.String || format.GetString() != ConfigFormat.Name)
            return Fail("No es un archivo de configuración de Aldune.");

        int version = root.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 1;
        string? app = root.TryGetProperty("app", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null;

        var sections = ConfigSections.None;
        var changes = new List<(ConfigField, object?, ConfigChange)>();
        foreach (var (name, section) in new[] { ("appearance", ConfigSections.Appearance), ("settings", ConfigSections.Settings) })
        {
            if (!root.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.Object) continue;
            sections |= section;
            foreach (var field in ConfigFields.All.Where(f => f.Section == section))
            {
                if (!node.TryGetProperty(field.Id, out var element)) continue;
                var (ok, value) = field.Read(element);
                if (!ok) continue;

                string before = field.Show(field.Get(current)), after = field.Show(value);
                if (before == after) continue;
                changes.Add((field, value, new ConfigChange(field.Id, section, before, after)));
            }
        }

        var plan = new ConfigImportPlan { IsValid = true, FileVersion = version, FileApp = app, SectionsInFile = sections };
        foreach (var (field, value, change) in changes) plan.Add(field, value, change);
        return plan;
    }

    private static ConfigImportPlan Fail(string error) => new() { IsValid = false, Error = error };
}
