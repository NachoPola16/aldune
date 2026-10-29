using System.Net;

namespace Aldune.SyncServer;

/// <summary>
/// Límite de intentos fallidos de token por dirección (ventana deslizante, solo en memoria). Sin él, quien llegue al puerto puede probar tokens
/// a ritmo ilimitado; con un token largo y aleatorio es inviable, pero los tokens cortos solo avisan en el log y no se rechazan.
/// Una dirección bloqueada recibe 429 aunque traiga el token bueno: si no, adivinar seguiría funcionando. Un acierto no borra los fallos:
/// solo los libera el paso del tiempo.
/// </summary>
public sealed class AuthFailureLimiter
{
    public const string MaxFailuresEnvVar = "ALDUNE_SYNC_MAX_AUTH_FAILURES_PER_MINUTE";
    public const string TrustProxyEnvVar = "ALDUNE_SYNC_TRUST_PROXY";
    public const int DefaultMaxFailures = 10;

    private readonly int _maxFailures;
    private readonly TimeSpan _window;
    private readonly int _maxTrackedAddresses;
    private readonly Dictionary<string, Queue<DateTimeOffset>> _failures = new();
    private readonly object _gate = new();

    public AuthFailureLimiter(int maxFailures, TimeSpan window, int maxTrackedAddresses = 10_000)
    {
        _maxFailures = maxFailures;
        _window = window;
        _maxTrackedAddresses = maxTrackedAddresses;
    }

    /// <summary>0 (o negativo) lo desactiva.</summary>
    public bool Enabled => _maxFailures > 0;

    public int TrackedAddresses
    {
        get { lock (_gate) return _failures.Count; }
    }

    /// <summary>Lee el máximo de la variable de entorno: ausente o no numérica = el de por defecto; 0 = desactivado.</summary>
    public static AuthFailureLimiter FromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable(MaxFailuresEnvVar);
        var max = int.TryParse(raw, out var parsed) ? parsed : DefaultMaxFailures;
        return new AuthFailureLimiter(max, TimeSpan.FromMinutes(1));
    }

    public static bool TrustProxyFromEnvironment() =>
        Environment.GetEnvironmentVariable(TrustProxyEnvVar) is "1" or "true" or "TRUE" or "True";

    /// <summary>
    /// Con el proxy de confianza, la última entrada de X-Forwarded-For (la que añadió él: las anteriores las puede escribir el cliente).
    /// Sin él, o si la cabecera falta o no es una IP, la dirección de la conexión. Sin dirección (solo en pruebas) una clave fija.
    /// </summary>
    public static string ClientAddress(string? forwardedFor, IPAddress? remote, bool trustProxy)
    {
        if (trustProxy && !string.IsNullOrWhiteSpace(forwardedFor))
        {
            var last = forwardedFor.Split(',')[^1].Trim();
            if (IPAddress.TryParse(last, out var ip)) return ip.ToString();
        }
        return remote?.ToString() ?? "unknown";
    }

    /// <summary>Cuánto falta para poder volver a intentarlo, o null si esa dirección no está bloqueada.</summary>
    public TimeSpan? RetryAfter(string address, DateTimeOffset now)
    {
        if (!Enabled) return null;
        lock (_gate)
        {
            if (!_failures.TryGetValue(address, out var queue)) return null;
            Prune(queue, now);
            if (queue.Count == 0)
            {
                _failures.Remove(address);
                return null;
            }
            return queue.Count >= _maxFailures ? queue.Peek() + _window - now : null;
        }
    }

    public void RecordFailure(string address, DateTimeOffset now)
    {
        if (!Enabled) return;
        lock (_gate)
        {
            if (!_failures.TryGetValue(address, out var queue))
            {
                if (_failures.Count >= _maxTrackedAddresses) Evict(now);
                queue = new Queue<DateTimeOffset>();
                _failures[address] = queue;
            }
            Prune(queue, now);
            queue.Enqueue(now);
        }
    }

    private void Prune(Queue<DateTimeOffset> queue, DateTimeOffset now)
    {
        while (queue.Count > 0 && now - queue.Peek() >= _window) queue.Dequeue();
    }

    /// <summary>Hace sitio: primero olvida lo caducado y, si sigue lleno, la dirección con el fallo más antiguo (un ataque con muchas direcciones no agota la memoria).</summary>
    private void Evict(DateTimeOffset now)
    {
        foreach (var address in _failures.Keys.ToList())
        {
            Prune(_failures[address], now);
            if (_failures[address].Count == 0) _failures.Remove(address);
        }
        if (_failures.Count < _maxTrackedAddresses) return;
        var oldest = _failures.MinBy(pair => pair.Value.Peek()).Key;
        _failures.Remove(oldest);
    }
}
