using System.Globalization;

namespace Aldune.Core;

/// <summary>Copia de <c>settings.json</c> antes de aplicar una importación, para poder volver atrás.</summary>
public static class ConfigBackup
{
    /// <summary>Copias que se guardan. Quien importa varias veces mientras ajusta un archivo compartido
    /// no debería acabar con una pila sin fin junto a sus ajustes; para volver atrás basta con las últimas.</summary>
    public const int MaxCopies = 5;

    private const string Marker = ".antes-de-importar-";

    public static string? Create(string settingsPath, DateTimeOffset now)
    {
        if (!File.Exists(settingsPath)) return null;

        var stamp = now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var baseName = $"{settingsPath}{Marker}{stamp}";
        // Dos importaciones en el mismo segundo no pisan la primera copia. El número va por encima del
        // más alto que haya, no al primer hueco: la poda pudo borrar la copia sin número, y reutilizar
        // ese nombre dejaría la copia nueva como la más antigua (y la siguiente poda se la llevaría).
        var sameSecond = Existing(settingsPath).Where(copy => copy.Key.Stamp == stamp).ToList();
        var target = sameSecond.Count == 0 ? baseName : $"{baseName}-{sameSecond.Max(copy => copy.Key.Suffix) + 1}";
        File.Copy(settingsPath, target);
        Prune(settingsPath);
        return target;
    }

    private static IEnumerable<(string Path, (string Stamp, int Suffix) Key)> Existing(string settingsPath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(settingsPath))!;
        var prefix = Path.GetFileName(settingsPath) + Marker;
        return Directory.GetFiles(dir, prefix + "*")
            .Select(path => (path, SortKey(Path.GetFileName(path)[prefix.Length..])));
    }

    private static void Prune(string settingsPath)
    {
        var old = Existing(settingsPath)
            .OrderByDescending(copy => copy.Key.Stamp, StringComparer.Ordinal)
            .ThenByDescending(copy => copy.Key.Suffix)
            .Skip(MaxCopies);

        foreach (var copy in old)
        {
            // Una copia que no se deja borrar (abierta en un editor) se queda: la importación ya está hecha.
            try { File.Delete(copy.Path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    // «20261004-093015-10»: la marca de tiempo ordena como texto, pero el sufijo de las del mismo segundo
    // hay que compararlo como número («-10» va detrás de «-9»).
    private static (string Stamp, int Suffix) SortKey(string tail)
    {
        const int stampLength = 15;   // yyyyMMdd-HHmmss
        if (tail.Length <= stampLength) return (tail, 1);
        return int.TryParse(tail[(stampLength + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? (tail[..stampLength], n)
            : (tail, 1);
    }
}
