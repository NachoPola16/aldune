namespace Aldune.Core;

/// <summary>Copia de <c>settings.json</c> antes de aplicar una importación, para poder volver atrás.</summary>
public static class ConfigBackup
{
    public static string? Create(string settingsPath, DateTimeOffset now)
    {
        if (!File.Exists(settingsPath)) return null;

        var baseName = $"{settingsPath}.antes-de-importar-{now:yyyyMMdd-HHmmss}";
        var target = baseName;
        // Dos importaciones en el mismo segundo no pisan la primera copia.
        for (int i = 2; File.Exists(target); i++) target = $"{baseName}-{i}";
        File.Copy(settingsPath, target);
        return target;
    }
}
