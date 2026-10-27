using System.Net;
using System.Text;

namespace Aldune.Core.Tests;

/// <summary>
/// Un objeto de WebDAV por encima del tope de respuesta (8 MB) se salta como uno ilegible: no puede hacer fallar la pasada entera,
/// porque entonces nadie podría volver a sincronizar hasta borrarlo a mano.
/// </summary>
public sealed class WebDavOversizeTests : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _url;
    private readonly byte[] _key = new byte[32];
    private readonly SyncEnvelope _good;
    private volatile bool _stallBody;
    private readonly Guid _hugeId = Guid.NewGuid();

    public WebDavOversizeTests()
    {
        var note = new Note { Id = Guid.NewGuid(), Text = "buena", Color = "#EBD38B", State = NoteState.Active, ScreenOrigin = "primary", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        _good = SyncEnvelopeCodec.CreateNote(note, _key, "device");

        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        _url = $"http://127.0.0.1:{port}/dav/";
        _listener.Prefixes.Add(_url);
        _listener.Start();
        _ = Task.Run(Serve);
    }

    public void Dispose() => _listener.Close();

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
        var request = context.Request;
        var response = context.Response;
        byte[] body;
        if (request.HttpMethod == "MKCOL")
        {
            response.StatusCode = 405;
            body = Array.Empty<byte>();
        }
        else if (request.HttpMethod == "PROPFIND")
        {
            response.StatusCode = 207;
            var xml = "<?xml version=\"1.0\"?><d:multistatus xmlns:d=\"DAV:\">" +
                      "<d:response><d:href>/dav/objects/</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop></d:propstat></d:response>" +
                      $"<d:response><d:href>/dav/objects/{_hugeId:N}.json</d:href><d:propstat><d:prop><d:resourcetype/></d:prop></d:propstat></d:response>" +
                      (_stallBody ? "<d:response><d:href>/dav/objects/parado.json</d:href><d:propstat><d:prop><d:resourcetype/></d:prop></d:propstat></d:response>" : "") +
                      $"<d:response><d:href>/dav/objects/{_good.NoteId:N}.json</d:href><d:propstat><d:prop><d:resourcetype/></d:prop></d:propstat></d:response>" +
                      "</d:multistatus>";
            body = Encoding.UTF8.GetBytes(xml);
        }
        else if (request.Url!.AbsolutePath.EndsWith("parado.json", StringComparison.Ordinal))
        {
            // Cabeceras y un trozo del cuerpo, y luego nada (la wifi cae sin cerrar la conexión, el NAS se duerme).
            response.SendChunked = true;
            await response.OutputStream.WriteAsync(new byte[] { (byte)'{' });
            await response.OutputStream.FlushAsync();
            await Task.Delay(TimeSpan.FromSeconds(15));
            return;
        }
        else if (request.Url!.AbsolutePath.EndsWith($"{_hugeId:N}.json", StringComparison.Ordinal))
        {
            // Sin Content-Length (por trozos): el tope tiene que saltar leyendo, no solo mirando la cabecera.
            response.SendChunked = true;
            body = new byte[(int)HttpSyncTransport.MaxResponseBytes + 1024];
        }
        else
        {
            body = SyncEnvelopeCodec.Serialize(_good);
        }
        try
        {
            await response.OutputStream.WriteAsync(body);
            response.Close();
        }
        catch (Exception)
        {
            // El cliente cortó la conexión al pasarse del tope.
        }
    }

    [Fact]
    public void ReadAll_GivesUpWhenTheBodyStallsAfterTheHeaders_InsteadOfHangingForever()
    {
        // Con ResponseHeadersRead el tiempo del HttpClient solo cubre hasta las cabeceras: leer el cuerpo tiene que estar acotado por su cuenta,
        // o una pasada colgada retiene el cerrojo de la sync y congela «Sincronizar ahora».
        _stallBody = true;
        var transport = new WebDavSyncTransport(_url, "user", "password", TimeSpan.FromMilliseconds(800));

        var task = Task.Run(() => transport.ReadAll());

        Assert.True(((IAsyncResult)task).AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(6)), "la lectura se quedó colgada");
        Assert.True(task.IsFaulted);
    }

    [Fact]
    public void ReadAll_SkipsAnObjectOverTheCap_AndKeepsTheRest()
    {
        var transport = new WebDavSyncTransport(_url, "user", "password");
        var objects = transport.ReadAll();
        Assert.Equal(new[] { _good.NoteId }, objects.Select(o => o.Envelope.NoteId));
        // El objeto enorme existe en el almacén aunque no se leyó: se avisa para que nadie lo trate como inexistente.
        Assert.Equal(new[] { _hugeId }, transport.UnreadableIds);
    }
}
