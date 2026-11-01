using System.Net;
using System.Text;
using Aldune.Core;

namespace Aldune.Core.Tests;

/// <summary>
/// The look document over the Folder and WebDAV transports (docs/specs/2026-11-01-sync-look-design.md): one file, <c>look.aldune</c>, in the root of
/// the store, next to <c>objects/</c>, never inside it: every installed version lists only <c>objects/*.json</c>.
/// </summary>
public sealed class SyncLookFolderAndWebDavTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-look-fw-{Guid.NewGuid():N}");

    public SyncLookFolderAndWebDavTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void AFolderWithoutALookDocumentReadsNull()
    {
        var transport = new FolderSyncTransport(Path.Combine(_root, "store"));

        Assert.Null(transport.ReadLook());
    }

    [Fact]
    public void TheFolderKeepsTheLookNextToObjectsAndNeverAmongThem()
    {
        var folder = Path.Combine(_root, "store");
        var transport = new FolderSyncTransport(folder);
        var blob = new byte[] { 1, 2, 3, 4 };

        transport.WriteLook(blob);

        Assert.Equal(blob, transport.ReadLook());
        Assert.True(File.Exists(Path.Combine(folder, "look.aldune")));
        Assert.Empty(Directory.GetFiles(Path.Combine(folder, "objects")));
        Assert.Empty(transport.ReadAll());
    }

    [Fact]
    public void WritingTheLookAgainReplacesItAndLeavesNoTemporaryFile()
    {
        var folder = Path.Combine(_root, "store");
        var transport = new FolderSyncTransport(folder);

        transport.WriteLook(new byte[] { 1 });
        transport.WriteLook(new byte[] { 2, 2 });

        Assert.Equal(new byte[] { 2, 2 }, transport.ReadLook());
        Assert.Equal(["look.aldune"], Directory.GetFiles(folder).Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void ALookDocumentOverTheSizeCapIsIgnoredNotRead()
    {
        var folder = Path.Combine(_root, "store");
        var transport = new FolderSyncTransport(folder);
        File.WriteAllBytes(Path.Combine(folder, "look.aldune"), new byte[70_000]);

        Assert.Null(transport.ReadLook());
    }

    [Fact]
    public void TwoDevicesSharingAFolderShareTheLook()
    {
        var shared = Path.Combine(_root, "shared");
        var a = NewFolderDevice("a", shared, syncLook: true);
        var b = NewFolderDevice("b", shared, syncLook: true, importCode: a.Sync.GetOrCreateSyncCode());
        a.Settings.Appearance = a.Settings.Appearance == AppearanceMode.Dark ? AppearanceMode.Light : AppearanceMode.Dark;
        var wanted = a.Settings.Appearance;

        Assert.True(a.Sync.Synchronize().Succeeded);
        Assert.True(b.Sync.Synchronize().Succeeded);

        Assert.Equal(wanted, b.Settings.Appearance);
        Assert.Equal(1, b.LookApplied);
    }

    [Fact]
    public void WithTheOptionOffTheFolderGetsNoLookFile()
    {
        var shared = Path.Combine(_root, "shared");
        var a = NewFolderDevice("a", shared, syncLook: false);
        a.Settings.Appearance = a.Settings.Appearance == AppearanceMode.Dark ? AppearanceMode.Light : AppearanceMode.Dark;

        Assert.True(a.Sync.Synchronize().Succeeded);

        Assert.False(File.Exists(Path.Combine(shared, "look.aldune")));
    }

    [Fact]
    public void WebDavReadsNullWhenThereIsNoLookAndRoundTripsOne()
    {
        using var dav = new FakeDav();
        var transport = new WebDavSyncTransport(dav.Url, "user", "pass");

        Assert.Null(transport.ReadLook());
        transport.WriteLook([9, 8, 7]);

        Assert.Equal(new byte[] { 9, 8, 7 }, transport.ReadLook());
        // At the root of the store (beside objects/), never inside it.
        Assert.Equal("/dav/look.aldune", dav.LookPath);
    }

    [Fact]
    public void AWebDavWithoutPutOnTheRootFailsLoudlyForTheCallerToSwallow()
    {
        using var dav = new FakeDav { RefuseLookWrites = true };
        var transport = new WebDavSyncTransport(dav.Url, "user", "pass");

        Assert.ThrowsAny<Exception>(() => transport.WriteLook([1]));
    }

    private sealed class Device
    {
        public required AppSettings Settings { get; init; }
        public required SyncService Sync { get; init; }
        public int LookApplied { get; set; }
    }

    private Device NewFolderDevice(string name, string shared, bool syncLook, string? importCode = null)
    {
        var settingsService = new SettingsService(Path.Combine(_root, name + ".json"));
        var settings = new AppSettings { SyncEnabled = true, SyncTransport = SyncTransportKind.Folder, SyncFolderPath = shared };
        var repository = new NotesRepository(new NotesDatabase(Path.Combine(_root, name + ".db")), new ContentCipher(new byte[32]));
        var sync = new SyncService(repository, settings, settingsService);
        if (importCode is null) _ = sync.GetOrCreateSyncCode(); else sync.ImportSyncCode(importCode);
        SyncProfileStore.GetActive(settings).SyncLook = syncLook;
        var device = new Device { Settings = settings, Sync = sync };
        sync.LookApplied += () => device.LookApplied++;
        return device;
    }

    /// <summary>A WebDAV that stores one file at <c>/dav/look.aldune</c> and answers the object calls with an empty store.</summary>
    private sealed class FakeDav : IDisposable
    {
        private readonly HttpListener _listener = new();
        private byte[]? _look;
        public string Url { get; }
        public string? LookPath { get; private set; }
        public bool RefuseLookWrites { get; init; }

        public FakeDav()
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            Url = $"http://127.0.0.1:{port}/dav/";
            _listener.Prefixes.Add(Url);
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
                try
                {
                    var request = context.Request;
                    var response = context.Response;
                    var path = request.Url!.AbsolutePath;
                    if (path.EndsWith("/look.aldune", StringComparison.Ordinal))
                    {
                        LookPath = path;
                        if (request.HttpMethod == "GET")
                        {
                            if (_look is null) response.StatusCode = 404; else await response.OutputStream.WriteAsync(_look);
                        }
                        else if (request.HttpMethod == "PUT" && !RefuseLookWrites)
                        {
                            using var ms = new MemoryStream();
                            await request.InputStream.CopyToAsync(ms);
                            _look = ms.ToArray();
                            response.StatusCode = 201;
                        }
                        else response.StatusCode = 403;
                    }
                    else if (request.HttpMethod == "MKCOL") response.StatusCode = 405;
                    else if (request.HttpMethod == "PROPFIND")
                    {
                        response.StatusCode = 207;
                        var xml = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><d:multistatus xmlns:d=\"DAV:\"><d:response><d:href>/dav/objects/</d:href>" +
                            "<d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop></d:propstat></d:response></d:multistatus>");
                        await response.OutputStream.WriteAsync(xml);
                    }
                    else response.StatusCode = 404;
                    response.Close();
                }
                catch (Exception) when (!_listener.IsListening) { }
            }
        }
    }
}
