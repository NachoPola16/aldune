using System.Text.Json;

namespace Aldune.Core;

public sealed class SettingsService
{
    private readonly string _settingsPath;

    public SettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    /// <summary>
    /// Al leer se aceptan los enums también por su nombre ("Dark", no solo 1): un settings.json
    /// editado a mano así lanzaba JsonException y la app no arrancaba. Al escribir se siguen usando
    /// números, que es lo único que entienden las versiones anteriores.
    /// </summary>
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
            return new AppSettings();

        var json = File.ReadAllText(_settingsPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, ReadOptions) ?? new AppSettings();
        // Un settings.json editado a mano no puede dejar temas que rompan el pintado de las notas.
        settings.CustomThemes = NoteThemes.Sanitize(settings.CustomThemes);
        SyncProfileStore.Ensure(settings);
        return settings;
    }

    public void Save(AppSettings settings)
    {
        SyncProfileStore.SaveActiveFromLegacy(settings);
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings);

        // Write to a temp file first, then atomically replace the real settings file. This file
        // holds the ONLY copy of the wrapped database encryption key, so a truncating in-place
        // write (File.WriteAllText) risks permanent key loss if the process crashes, loses power,
        // or hits a full disk mid-write. File.Move(..., overwrite: true) is atomic on the same volume.
        var tmpPath = _settingsPath + ".tmp";
        try
        {
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, _settingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);
        }
    }
}
