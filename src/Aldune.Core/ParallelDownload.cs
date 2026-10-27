namespace Aldune.Core;

/// <summary>
/// Descarga los objetos del almacén remoto de varios en varios. Cada pasada de sync lee el almacén entero, y bajar las notas de
/// una en una multiplicaba la latencia por su número aunque nada hubiera cambiado. El orden del resultado es el de la entrada.
/// </summary>
internal static class ParallelDownload
{
    /// <summary>Peticiones a la vez: bastan para tapar la latencia sin saturar a un servidor doméstico ni a un WebDAV compartido.</summary>
    internal const int MaxConcurrent = 8;

    /// <summary>
    /// Un resultado null se descarta (un archivo ilegible no impide el resto). El primer fallo sale tal cual, sin envolver en
    /// <see cref="AggregateException"/>: <see cref="SyncService"/> distingue los fallos esperados por su tipo.
    /// </summary>
    internal static List<TResult> Run<TItem, TResult>(IReadOnlyList<TItem> items, Func<TItem, Task<TResult?>> fetch)
        where TResult : class
    {
        using var gate = new SemaphoreSlim(MaxConcurrent);
        // Tras el primer fallo (un 401, un portal cautivo, un timeout) las que siguen en cola no empiezan: si no, un almacén de
        // cientos de notas seguía golpeando al servidor antes de devolver el error.
        var failed = false;
        var tasks = items.Select(async item =>
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (Volatile.Read(ref failed)) return null;
                return await fetch(item).ConfigureAwait(false);
            }
            catch
            {
                Volatile.Write(ref failed, true);
                throw;
            }
            finally { gate.Release(); }
        }).ToList();
        var results = Task.WhenAll(tasks).GetAwaiter().GetResult();
        return results.Where(result => result is not null).ToList()!;
    }
}
