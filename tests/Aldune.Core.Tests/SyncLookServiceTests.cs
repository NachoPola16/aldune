using System.Net;
using Aldune.Core;

namespace Aldune.Core.Tests;

/// <summary>Two devices syncing the look through a store (a tiny in-memory server that speaks the object and look routes).</summary>
public sealed class SyncLookServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-look-{Guid.NewGuid():N}");
    private readonly HttpListener _listener = new();
    private readonly string _url;
    private byte[]? _look;
    private bool _knowsLookRoute = true;

    public SyncLookServiceTests()
    {
        Directory.CreateDirectory(_root);
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
        _listener.Stop();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                var path = context.Request.Url!.AbsolutePath;
                var response = context.Response;
                if (path == "/api/v1/objects") await Body(response, "[]", "application/json");
                else if (path == "/api/v1/look" && _knowsLookRoute && context.Request.HttpMethod == "GET")
                {
                    if (_look is null) response.StatusCode = 404; else await response.OutputStream.WriteAsync(_look);
                }
                else if (path == "/api/v1/look" && _knowsLookRoute && context.Request.HttpMethod == "PUT")
                {
                    using var ms = new MemoryStream();
                    await context.Request.InputStream.CopyToAsync(ms);
                    _look = ms.ToArray();
                    response.StatusCode = 204;
                }
                else response.StatusCode = 404;
                response.Close();
            }
            catch (Exception) when (!_listener.IsListening) { }
        }
    }

    private static async Task Body(HttpListenerResponse response, string text, string type)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        response.ContentType = type;
        await response.OutputStream.WriteAsync(bytes);
    }

    private sealed class Device
    {
        public required AppSettings Settings { get; init; }
        public required SyncService Sync { get; init; }
        public int LookApplied { get; set; }
    }

    private Device NewDevice(string name, bool syncLook, string? importCode = null)
    {
        var settingsService = new SettingsService(Path.Combine(_root, name + ".json"));
        var settings = new AppSettings { SyncEnabled = true, SyncTransport = SyncTransportKind.Server, SyncServerUrl = _url };
        var repository = new NotesRepository(new NotesDatabase(Path.Combine(_root, name + ".db")), new ContentCipher(new byte[32]));
        var sync = new SyncService(repository, settings, settingsService);
        sync.SetServerToken("a-token-that-is-long-enough-for-tests");
        if (importCode is null) _ = sync.GetOrCreateSyncCode(); else sync.ImportSyncCode(importCode);
        SyncProfileStore.GetActive(settings).SyncLook = syncLook;
        var device = new Device { Settings = settings, Sync = sync };
        sync.LookApplied += () => device.LookApplied++;
        return device;
    }

    private static AppearanceMode Other(AppearanceMode mode) => mode == AppearanceMode.Dark ? AppearanceMode.Light : AppearanceMode.Dark;

    [Fact]
    public void ALookChangedOnOneDeviceReachesTheOther()
    {
        var a = NewDevice("a", syncLook: true);
        var b = NewDevice("b", syncLook: true, importCode: a.Sync.GetOrCreateSyncCode());
        a.Settings.Appearance = Other(b.Settings.Appearance);
        var wanted = a.Settings.Appearance;

        Assert.True(a.Sync.Synchronize().Succeeded);
        Assert.True(b.Sync.Synchronize().Succeeded);

        Assert.Equal(wanted, b.Settings.Appearance);
        Assert.Equal(1, b.LookApplied);
    }

    [Fact]
    public void WithTheOptionOffNothingTravelsInEitherDirection()
    {
        var a = NewDevice("a", syncLook: false);
        var b = NewDevice("b", syncLook: true, importCode: a.Sync.GetOrCreateSyncCode());
        var original = b.Settings.Appearance;
        a.Settings.Appearance = Other(original);

        Assert.True(a.Sync.Synchronize().Succeeded);
        Assert.True(b.Sync.Synchronize().Succeeded);

        Assert.Equal(original, b.Settings.Appearance);
        Assert.Equal(0, b.LookApplied);
    }

    [Fact]
    public void ALaterChangeOnTheOtherDeviceComesBack()
    {
        var a = NewDevice("a", syncLook: true);
        var b = NewDevice("b", syncLook: true, importCode: a.Sync.GetOrCreateSyncCode());
        a.Settings.Appearance = Other(a.Settings.Appearance);
        a.Sync.Synchronize();
        b.Sync.Synchronize();

        Thread.Sleep(20);
        b.Settings.Appearance = Other(b.Settings.Appearance);
        var wanted = b.Settings.Appearance;
        b.Sync.Synchronize();
        a.Sync.Synchronize();

        Assert.Equal(wanted, a.Settings.Appearance);
    }

    [Fact]
    public void AServerWithoutTheLookRouteNeverFailsTheNotesSync()
    {
        _knowsLookRoute = false;
        var a = NewDevice("a", syncLook: true);
        a.Settings.Appearance = Other(a.Settings.Appearance);

        var result = a.Sync.Synchronize();

        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public void PerDeviceSettingsAreNotTouched()
    {
        var a = NewDevice("a", syncLook: true);
        var b = NewDevice("b", syncLook: true, importCode: a.Sync.GetOrCreateSyncCode());
        a.Settings.Appearance = Other(a.Settings.Appearance);
        b.Settings.Language = "de";
        a.Sync.Synchronize();

        b.Sync.Synchronize();

        Assert.Equal("de", b.Settings.Language);
    }
}
