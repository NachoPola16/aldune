using System.Net;
using System.Text;

namespace Aldune.Core.Tests;

/// <summary>An object that another device deleted between the folder listing and its download (a key rotation, a delete) is simply gone: it must not fail the whole pass.</summary>
public sealed class WebDavVanishedObjectTests : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _url;
    private readonly SyncEnvelope _good;
    private readonly Guid _goneId = Guid.NewGuid();

    public WebDavVanishedObjectTests()
    {
        var note = new Note { Id = Guid.NewGuid(), Text = "buena", Color = "#EBD38B", State = NoteState.Active, ScreenOrigin = "primary", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        _good = SyncEnvelopeCodec.CreateNote(note, new byte[32], "device");
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
        byte[] body = Array.Empty<byte>();
        if (request.HttpMethod == "MKCOL") response.StatusCode = 405;
        else if (request.HttpMethod == "PROPFIND")
        {
            response.StatusCode = 207;
            body = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><d:multistatus xmlns:d=\"DAV:\">" +
                "<d:response><d:href>/dav/objects/</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop></d:propstat></d:response>" +
                $"<d:response><d:href>/dav/objects/{_goneId:N}.json</d:href><d:propstat><d:prop><d:resourcetype/></d:prop></d:propstat></d:response>" +
                $"<d:response><d:href>/dav/objects/{_good.NoteId:N}.json</d:href><d:propstat><d:prop><d:resourcetype/></d:prop></d:propstat></d:response>" +
                "</d:multistatus>");
        }
        else if (request.Url!.AbsolutePath.Contains(_goneId.ToString("N"), StringComparison.Ordinal)) response.StatusCode = 404;
        else { response.StatusCode = 200; body = SyncEnvelopeCodec.Serialize(_good); }
        response.ContentLength64 = body.Length;
        await response.OutputStream.WriteAsync(body);
        response.Close();
    }

    [Fact]
    public void ReadAll_SkipsAnObjectThatReturnsNotFound()
    {
        var transport = new WebDavSyncTransport(_url, "user", "pass");

        var all = transport.ReadAll();

        Assert.Equal([_good.NoteId], all.Select(o => o.Envelope.NoteId));
    }
}
