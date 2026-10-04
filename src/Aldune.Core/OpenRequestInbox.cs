namespace Aldune.Core;

/// <summary>
/// Rutas que una segunda Aldune ("Abrir con" en el Explorador) deja para la que ya está en marcha. La segunda
/// escribe aquí y avisa con el evento de <c>SingleInstance</c>; la primera vacía el buzón al recibir el aviso.
/// Un archivo por envío, nombrado por fecha para conservar el orden.
/// </summary>
public static class OpenRequestInbox
{
    public static void Post(string inboxDirectory, IEnumerable<string> paths)
    {
        Directory.CreateDirectory(inboxDirectory);
        var name = $"{DateTime.UtcNow:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}.txt";
        var temporary = Path.Combine(inboxDirectory, name + ".part");
        File.WriteAllLines(temporary, paths);
        // Renombrar al final: quien vacía el buzón nunca lee un envío a medio escribir.
        File.Move(temporary, Path.Combine(inboxDirectory, name));
    }

    public static IReadOnlyList<string> Drain(string inboxDirectory)
    {
        if (!Directory.Exists(inboxDirectory)) return [];
        var paths = new List<string>();
        foreach (var file in Directory.GetFiles(inboxDirectory, "*.txt").Order(StringComparer.Ordinal))
        {
            try
            {
                paths.AddRange(File.ReadAllLines(file).Where(line => !string.IsNullOrWhiteSpace(line)));
                File.Delete(file);
            }
            catch (IOException) { }   // otro proceso lo tiene: se leerá en el siguiente aviso
        }
        return paths;
    }
}
