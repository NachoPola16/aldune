using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace Aldune.Core.Tests;

/// <summary>
/// Cada pasada de sync lee todo el almacén. Con un servidor remoto, bajar las notas una a una multiplica la latencia por
/// el número de notas aunque no haya cambiado nada: 200 notas a 50 ms son 10 s. Aquí se fija que la descarga va en
/// paralelo, completa y en el orden del listado.
/// </summary>
public sealed class SyncTransportParallelDownloadTests : IDisposable
{
    private const int Notes = 40;
    private const int ObjectLatencyMs = 50;

    private readonly HttpListener _listener = new();
    private readonly string _url;
    private readonly byte[] _key = new byte[32];
    private readonly List<SyncEnvelope> _envelopes = new();
    private int _inFlight;
    private int _maxInFlight;

    public SyncTransportParallelDownloadTests()
    {
        for (int i = 0; i < Notes; i++)
        {
            var note = new Note { Id = Guid.NewGuid(), Text = $"nota {i}", Color = "#EBD38B", State = NoteState.Active, ScreenOrigin = "primary", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            _envelopes.Add(SyncEnvelopeCodec.CreateNote(note, _key, "device"));
        }

        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        _url = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(_url);
        _listener.Start();
        _ = Task.Run(Serve);
    }

    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch (Exception) when (!_listener.IsListening) { return; }
            _ = Task.Run(() => Handle(context));
        }
    }

    private async Task Handle(HttpListenerContext context)
    {
        var path = context.Request.Url!.AbsolutePath;
        byte[] body;
        if (path.EndsWith("/objects", StringComparison.Ordinal))
        {
            body = JsonSerializer.SerializeToUtf8Bytes(_envelopes.Select(e => new
            {
                noteId = e.NoteId, tombstone = e.Tombstone, updatedAt = e.UpdatedAt, deviceId = e.DeviceId,
            }));
        }
        else
        {
            int now = Interlocked.Increment(ref _inFlight);
            int seen;
            while (now > (seen = Volatile.Read(ref _maxInFlight))) Interlocked.CompareExchange(ref _maxInFlight, now, seen);
            await Task.Delay(ObjectLatencyMs);
            Interlocked.Decrement(ref _inFlight);
            var id = Guid.Parse(path[(path.LastIndexOf('/') + 1)..]);
            body = SyncEnvelopeCodec.Serialize(_envelopes.Single(e => e.NoteId == id));
        }
        context.Response.ContentType = "application/json";
        await context.Response.OutputStream.WriteAsync(body);
        context.Response.Close();
    }

    [Fact]
    public void ReadAll_DownloadsObjectsInParallel_CompleteAndInListOrder()
    {
        var transport = new HttpSyncTransport(_url, "a-token-that-is-long-enough-for-tests");

        var result = transport.ReadAll();

        Assert.Equal(_envelopes.Select(e => e.NoteId), result.Select(r => r.Envelope.NoteId));
        Assert.True(_maxInFlight > 1, $"las peticiones iban de una en una (máximo simultáneo: {_maxInFlight})");
        // Sin comprobar el tiempo total: con toda la suite en marcha el equipo va cargado y tardaba más que en serie (3,2 s con un tope de 1,7 s);
        // el paralelismo lo prueba el máximo simultáneo, que no depende de la carga.
        Assert.True(_maxInFlight <= ParallelDownload.MaxConcurrent, $"pasó del tope de peticiones simultáneas: {_maxInFlight}");
    }

    public void Dispose() => _listener.Close();
}
