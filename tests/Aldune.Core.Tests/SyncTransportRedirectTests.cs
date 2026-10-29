using System.Net;
using System.Net.Sockets;
using Aldune.Core;

namespace Aldune.Core.Tests;

/// <summary>
/// Un servidor de sync (o un WebDAV) que responde con una redirección a OTRO host no debe recibir el token ni la contraseña:
/// quien controle la redirección (un servidor comprometido, un proxy en el camino) se llevaría las credenciales.
/// «127.0.0.1» y «localhost» son hosts distintos para HTTP aunque lleguen al mismo equipo.
/// </summary>
public sealed class SyncTransportRedirectTests : IDisposable
{
    private readonly HttpListener _origin = new();
    private readonly HttpListener _other = new();
    private readonly List<Task> _loops = new();
    private readonly List<string?> _authAtOther = new();
    private readonly List<string?> _authAtOriginAfterRedirect = new();
    private readonly string _originUrl;
    private readonly string _otherUrl;

    public SyncTransportRedirectTests()
    {
        var originPort = FreePort();
        var otherPort = FreePort();
        _originUrl = $"http://127.0.0.1:{originPort}/";
        _otherUrl = $"http://localhost:{otherPort}/";
        _origin.Prefixes.Add(_originUrl);
        _other.Prefixes.Add(_otherUrl);
        _origin.Start();
        _other.Start();
        _loops.Add(Task.Run(() => Serve(_origin, context =>
        {
            // Todo lo que llega al origen se redirige al otro host, conservando la ruta.
            context.Response.StatusCode = 307;
            context.Response.Headers["Location"] = _otherUrl.TrimEnd('/') + context.Request.RawUrl;
        })));
        _loops.Add(Task.Run(() => Serve(_other, context =>
        {
            lock (_authAtOther) _authAtOther.Add(context.Request.Headers["Authorization"]);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            var body = "[]"u8.ToArray();
            context.Response.OutputStream.Write(body);
        })));
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void Serve(HttpListener listener, Action<HttpListenerContext> handler)
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try { context = listener.GetContext(); }
            catch (Exception) { return; }
            try { handler(context); }
            finally { context.Response.Close(); }
        }
    }

    public void Dispose()
    {
        _origin.Close();
        _other.Close();
    }

    private void AssertTheOtherHostWasReachedWithoutCredentials()
    {
        lock (_authAtOther)
        {
            // Si el cliente no siguiera la redirección el test no probaría nada: tiene que haber llegado.
            Assert.NotEmpty(_authAtOther);
            Assert.All(_authAtOther, header => Assert.Null(header));
        }
    }

    [Fact]
    public void HttpTransport_DoesNotSendTheTokenToAnotherHostAfterARedirect()
    {
        var transport = new HttpSyncTransport(_originUrl, "secret-token", TimeSpan.FromSeconds(5));

        try { transport.ReadAll(); } catch (Exception) { /* da igual cómo acabe: lo que cuenta es lo que vio el otro host */ }

        AssertTheOtherHostWasReachedWithoutCredentials();
    }

    [Fact]
    public void WebDavTransport_DoesNotSendThePasswordToAnotherHostAfterARedirect()
    {
        var transport = new WebDavSyncTransport(_originUrl, "ana", "secret-password", TimeSpan.FromSeconds(5));

        try { transport.ReadAll(); } catch (Exception) { }

        AssertTheOtherHostWasReachedWithoutCredentials();
    }
}
