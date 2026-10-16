using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>
/// Un servidor que acepta la conexión y no contesta nunca (una VPN caída a medias, un NAS dormido). El cliente HTTP
/// de los transportes corta a los 20 s; aquí se acorta por constructor para no esperar tanto en cada ejecución.
/// </summary>
public sealed class SyncTransportTimeoutTests : IDisposable
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(300);
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-sync-timeout-{Guid.NewGuid():N}");
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly List<TcpClient> _held = [];
    private readonly string _url;

    public SyncTransportTimeoutTests()
    {
        Directory.CreateDirectory(_root);
        _listener.Start();
        _url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";
        _ = Task.Run(AcceptAndStaySilent);
    }

    // Se guardan las conexiones aceptadas para que no se cierren: un cierre daría un error inmediato, no un timeout.
    private async Task AcceptAndStaySilent()
    {
        try
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync();
                lock (_held) _held.Add(client);
            }
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException) { }
    }

    [Fact]
    public void HttpTransport_WaitsTwentySecondsByDefault()
    {
        Assert.Equal(TimeSpan.FromSeconds(20), HttpSyncTransport.DefaultTimeout);
        Assert.Equal(HttpSyncTransport.DefaultTimeout, new HttpSyncTransport(_url, "token").Timeout);
        Assert.Equal(HttpSyncTransport.DefaultTimeout, new WebDavSyncTransport(_url, "user", "password").Timeout);
    }

    [Fact]
    public void HttpTransport_GivesUpAfterTheConfiguredTimeout()
    {
        var transport = new HttpSyncTransport(_url, "token", ShortTimeout);
        var clock = Stopwatch.StartNew();

        Assert.ThrowsAny<OperationCanceledException>(() => transport.ReadAll());
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"Tardó {clock.Elapsed}.");
    }

    [Fact]
    public void WebDavTransport_GivesUpAfterTheConfiguredTimeout()
    {
        var transport = new WebDavSyncTransport(_url, "user", "password", ShortTimeout);
        var clock = Stopwatch.StartNew();

        Assert.ThrowsAny<OperationCanceledException>(() => transport.ReadAll());
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"Tardó {clock.Elapsed}.");
    }

    [Fact]
    public void Synchronize_AgainstAServerThatNeverAnswers_ReturnsAFailureInsteadOfThrowing()
    {
        var databasePath = Path.Combine(_root, "notes.db");
        var settingsService = new SettingsService(Path.Combine(_root, "settings.json"));
        var settings = new AppSettings
        {
            SyncEnabled = true,
            SyncTransport = SyncTransportKind.Server,
            SyncServerUrl = _url
        };
        var repository = new NotesRepository(new NotesDatabase(databasePath), new ContentCipher(new byte[32]));
        var sync = new SyncService(repository, settings, settingsService, ShortTimeout);
        sync.SetServerToken("a-token-that-is-long-enough-for-tests");
        sync.GetOrCreateSyncCode();
        var clock = Stopwatch.StartNew();

        var result = sync.Synchronize();

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"Tardó {clock.Elapsed}.");
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString());
        SqliteConnection.ClearPool(connection);
    }

    public void Dispose()
    {
        _listener.Stop();
        lock (_held) foreach (var client in _held) client.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
