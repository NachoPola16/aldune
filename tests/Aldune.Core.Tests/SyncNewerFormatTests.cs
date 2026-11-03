using System.Net;
using System.Text;

namespace Aldune.Core.Tests;

/// <summary>
/// An object written by a newer sync format (a future 5) is not a damaged one. Skipping it made this older version believe the note did not
/// exist and upload over it, dropping the fields it does not know. Every transport now stops the pass with an "update Aldune" message and
/// never writes over it, which is also what the Android HTTP client does.
/// </summary>
public sealed class SyncNewerFormatTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-newer-format-{Guid.NewGuid():N}");
    private readonly HttpListener _listener = new();
    private readonly string _url;
    private readonly byte[] _newer;
    private readonly Guid _id = Guid.NewGuid();

    public SyncNewerFormatTests()
    {
        Directory.CreateDirectory(_dir);
        var note = new Note { Id = _id, Text = "from the future", Color = "#EBD38B", State = NoteState.Active, ScreenOrigin = "primary", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var envelope = SyncEnvelopeCodec.CreateNote(note, new byte[32], "device-from-the-future");
        envelope.Format = SyncCompatibility.CurrentFormat + 1; // Serialize does not validate: this is what a newer client would write
        _newer = SyncEnvelopeCodec.Serialize(envelope);

        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        _url = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(_url);
        _listener.Start();
        _ = Task.Run(Serve);
    }

    public void Dispose()
    {
        _listener.Close();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
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
        var request = context.Request;
        var response = context.Response;
        var path = request.Url!.AbsolutePath;
        byte[] body = Array.Empty<byte>();
        if (request.HttpMethod == "MKCOL") response.StatusCode = 405;
        else if (request.HttpMethod == "PROPFIND")
        {
            response.StatusCode = 207;
            body = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><d:multistatus xmlns:d=\"DAV:\">" +
                "<d:response><d:href>/dav/objects/</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop></d:propstat></d:response>" +
                $"<d:response><d:href>/dav/objects/{_id:N}.json</d:href><d:propstat><d:prop><d:resourcetype/></d:prop></d:propstat></d:response>" +
                "</d:multistatus>");
        }
        else if (request.HttpMethod == "PUT")
        {
            response.StatusCode = 409;
            body = Encoding.UTF8.GetBytes(SyncCompatibility.UnsupportedFormatMessage(SyncCompatibility.CurrentFormat + 1));
        }
        else if (path.EndsWith("/api/v1/objects", StringComparison.Ordinal))
        {
            body = Encoding.UTF8.GetBytes($"[{{\"noteId\":\"{_id}\",\"tombstone\":false,\"updatedAt\":\"{DateTimeOffset.UtcNow:O}\",\"deviceId\":\"d\",\"format\":5}}]");
        }
        else body = _newer;
        response.ContentLength64 = body.Length;
        await response.OutputStream.WriteAsync(body);
        response.Close();
    }

    private string WriteNewerToFolder()
    {
        var objects = Path.Combine(_dir, "objects");
        Directory.CreateDirectory(objects);
        var path = Path.Combine(objects, $"{_id:N}.json");
        File.WriteAllBytes(path, _newer);
        return path;
    }

    [Fact]
    public void Folder_ReadAll_FailsWithAnUpdateMessageInsteadOfSkippingTheObject()
    {
        WriteNewerToFolder();

        var error = Assert.Throws<UnsupportedFormatException>(() => new FolderSyncTransport(_dir).ReadAll());

        Assert.Contains("newer Aldune", error.Message);
    }

    [Fact]
    public void Folder_Write_NeverReplacesAnObjectOfANewerFormat()
    {
        var path = WriteNewerToFolder();
        var older = SyncEnvelopeCodec.CreateNote(
            new Note { Id = _id, Text = "old", Color = "#EBD38B", State = NoteState.Active, ScreenOrigin = "primary", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow.AddHours(1) },
            new byte[32], "old-device");

        Assert.Throws<UnsupportedFormatException>(() => new FolderSyncTransport(_dir).Write(older));

        Assert.Equal(_newer, File.ReadAllBytes(path));
    }

    [Fact]
    public void Folder_ADamagedObjectIsStillSkipped()
    {
        var objects = Path.Combine(_dir, "objects");
        Directory.CreateDirectory(objects);
        File.WriteAllText(Path.Combine(objects, $"{Guid.NewGuid():N}.json"), "{ not json");

        Assert.Empty(new FolderSyncTransport(_dir).ReadAll());
    }

    [Fact]
    public void ASyncPass_StopsWithAClearMessageAndLeavesTheNewerObjectAlone()
    {
        var path = WriteNewerToFolder();
        var repository = new NotesRepository(new NotesDatabase(Path.Combine(_dir, "notes.db")), new ContentCipher(new byte[32]));
        var settings = new AppSettings { SyncEnabled = true, SyncTransport = SyncTransportKind.Folder, SyncFolderPath = _dir };
        var service = new SyncService(repository, settings, new SettingsService(Path.Combine(_dir, "settings.json")));
        service.GetOrCreateSyncCode();
        repository.Create("local note", "#EBD38B", "primary");

        var result = service.Synchronize();

        Assert.False(result.Succeeded);
        Assert.Contains("newer Aldune", result.Error);
        Assert.Equal(_newer, File.ReadAllBytes(path));
        Assert.Single(Directory.GetFiles(Path.Combine(_dir, "objects"), "*.json")); // the local note was not published either
    }

    [Fact]
    public void Http_ReadAll_FailsWithAnUpdateMessage()
    {
        using var transport = new HttpSyncTransport(_url, "a-token-that-is-long-enough-for-tests");

        var error = Assert.Throws<UnsupportedFormatException>(() => transport.ReadAll());

        Assert.Contains("newer Aldune", error.Message);
    }

    [Fact]
    public void Http_Write_ReportsTheServersRefusalToReplaceANewerObject()
    {
        using var transport = new HttpSyncTransport(_url, "a-token-that-is-long-enough-for-tests");
        var envelope = SyncEnvelopeCodec.CreateTombstone(new SyncTombstone(_id, DateTimeOffset.UtcNow, "d"), new byte[32], "d");

        var error = Assert.Throws<UnsupportedFormatException>(() => transport.Write(envelope));

        Assert.Contains("newer Aldune", error.Message);
    }

    [Fact]
    public void WebDav_ReadAll_FailsWithAnUpdateMessageInsteadOfSkippingTheObject()
    {
        using var transport = new WebDavSyncTransport(_url + "dav/", "user", "pass");

        var error = Assert.Throws<UnsupportedFormatException>(() => transport.ReadAll());

        Assert.Contains("newer Aldune", error.Message);
    }
}
