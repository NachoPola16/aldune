using System.Text.Json;

namespace Fanote.Core;

public sealed class SettingsService
{
    private readonly string _settingsPath;

    public SettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
            return new AppSettings();

        var json = File.ReadAllText(_settingsPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
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
