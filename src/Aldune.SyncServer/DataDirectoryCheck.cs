namespace Aldune.SyncServer;

public static class DataDirectoryCheck
{
    /// <summary>
    /// Falla al arrancar si el servidor no puede escribir en su carpeta de datos. El contenedor corre como usuario no root (UID 1654) y un
    /// volumen creado por una versión anterior es de root: sin esta comprobación el servidor arrancaba «sano» (leía) y cada PUT o DELETE
    /// de todos los clientes devolvía 500 hasta que alguien adivinaba el `chown`.
    /// </summary>
    public static void EnsureWritable(string directory)
    {
        var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            throw new InvalidOperationException(
                $"The server cannot write to its data directory '{directory}' ({ex.Message}). If the volume was created by an older image that ran as root, " +
                "run once: docker run --rm -v <volume>:/data alpine chown -R 1654:1654 /data (see docs/SYNC.md).", ex);
        }
    }
}
