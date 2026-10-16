using System.Net;
using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>
/// La sincronización automática corre en un hilo del pool sin ningún try/catch alrededor: una excepción que
/// <see cref="SyncService.Synchronize"/> deje escapar tumba la aplicación entera. Aquí se comprueba que un
/// servidor que responde algo inesperado acaba en un fallo normal (SyncResult con error), no en una excepción.
/// </summary>
public sealed class SyncServiceFailureTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-sync-failure-{Guid.NewGuid():N}");
    private readonly string _databasePath;
    private readonly HttpListener _listener = new();
    private readonly string _url;

    public SyncServiceFailureTests()
    {
        Directory.CreateDirectory(_root);
        _databasePath = Path.Combine(_root, "notes.db");
        int port = FreePort();
        _url = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(_url);
        _listener.Start();
        _ = Task.Run(Serve);
    }

    private static int FreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    // Lo que devuelve un portal cautivo o una página de error de un proxy: 200 con HTML, no JSON.
    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                var body = System.Text.Encoding.UTF8.GetBytes("<html><body>Please sign in</body></html>");
                context.Response.ContentType = "text/html";
                await context.Response.OutputStream.WriteAsync(body);
                context.Response.Close();
            }
            catch (Exception) when (!_listener.IsListening) { }
        }
    }

    [Fact]
    public void ServerAnsweringHtml_ReturnsAFailureInsteadOfThrowing()
    {
        var settingsService = new SettingsService(Path.Combine(_root, "settings.json"));
        var settings = new AppSettings
        {
            SyncEnabled = true,
            SyncTransport = SyncTransportKind.Server,
            SyncServerUrl = _url
        };
        var repository = new NotesRepository(new NotesDatabase(_databasePath), new ContentCipher(new byte[32]));
        var sync = new SyncService(repository, settings, settingsService);
        sync.SetServerToken("a-token-that-is-long-enough-for-tests");
        sync.GetOrCreateSyncCode();

        var result = sync.Synchronize();

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    public void Dispose()
    {
        _listener.Close();
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString());
        SqliteConnection.ClearPool(connection);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
