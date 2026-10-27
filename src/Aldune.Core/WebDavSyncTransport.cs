using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Aldune.Core;

/// <summary>
/// Transporte WebDAV para Nextcloud, ownCloud y servidores WebDAV compatibles.
/// Solo sube sobres cifrados; la clave de sincronización nunca sale del dispositivo.
/// </summary>
public sealed class WebDavSyncTransport : ISyncTransport
{
    private static readonly XNamespace Dav = "DAV:";
    private readonly HttpClient _client;
    private readonly Uri _objectsUri;
    private bool _collectionReady;

    public WebDavSyncTransport(string serverUrl, string username, string password, TimeSpan? timeout = null)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(serverUrl), UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme is not "http" and not "https"))
            throw new ArgumentException("The WebDAV URL must be an absolute HTTP(S) URL.", nameof(serverUrl));
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("A WebDAV username is required.", nameof(username));
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("A WebDAV password is required.", nameof(password));

        _objectsUri = new Uri(baseUri, "objects/");
        _client = new HttpClient
        {
            Timeout = timeout ?? HttpSyncTransport.DefaultTimeout,
            MaxResponseContentBufferSize = HttpSyncTransport.MaxResponseBytes
        };
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    internal TimeSpan Timeout => _client.Timeout;

    internal long MaxResponseBufferSize => _client.MaxResponseContentBufferSize;

    private System.Collections.Concurrent.ConcurrentBag<Guid> _unreadable = new();

    /// <inheritdoc />
    public IReadOnlyCollection<Guid> UnreadableIds => _unreadable.ToArray();

    public IReadOnlyList<SyncRemoteObject> ReadAll()
    {
        EnsureCollection();
        _unreadable = new System.Collections.Concurrent.ConcurrentBag<Guid>();
        var unreadable = _unreadable;
        using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), _objectsUri)
        {
            Content = new StringContent(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><d:propfind xmlns:d=\"DAV:\"><d:prop><d:resourcetype/></d:prop></d:propfind>",
                Encoding.UTF8,
                "application/xml")
        };
        request.Headers.TryAddWithoutValidation("Depth", "1");

        using var response = _client.SendAsync(request).GetAwaiter().GetResult();
        EnsureSuccess(response);
        XDocument document;
        try
        {
            document = XDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }
        catch (XmlException ex)
        {
            throw new InvalidOperationException("El servidor WebDAV devolvió un listado XML no válido.", ex);
        }
        var fileUris = new List<Uri>();
        foreach (var resource in document.Descendants(Dav + "response"))
        {
            var href = resource.Element(Dav + "href")?.Value;
            if (string.IsNullOrWhiteSpace(href) || resource.Descendants(Dav + "collection").Any()) continue;

            // Un href que apunte a otro servidor no se sigue: la petición lleva el usuario y la contraseña.
            if (ResolveHref(_objectsUri, href) is not { } fileUri) continue;
            if (!fileUri.AbsolutePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            fileUris.Add(fileUri);
        }

        return ParallelDownload.Run(fileUris, async fileUri =>
        {
            try
            {
                // Cabeceras primero y lectura propia con tope: el tope del HttpClient hacía fallar la pasada entera con un solo objeto
                // enorme, y en WebDAV (sin el límite del servidor propio) cualquiera con escritura podía bloquear la sync de todos.
                // El tiempo del HttpClient solo llega hasta las cabeceras con ResponseHeadersRead: la petición entera, cuerpo incluido, se acota
                // con el mismo plazo, o un servidor que se para a mitad (wifi caída sin cerrar, NAS dormido) colgaría la sync para siempre.
                using var deadline = new CancellationTokenSource(_client.Timeout);
                using var fileResponse = await _client.GetAsync(fileUri, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
                EnsureSuccess(fileResponse);
                var bytes = await ReadCappedAsync(fileResponse.Content, deadline.Token).ConfigureAwait(false);
                if (bytes is null)
                {
                    // Existe pero pasa del tope: se salta, y se avisa de su id para que la sync no lo dé por inexistente y suba encima.
                    if (Guid.TryParse(Path.GetFileNameWithoutExtension(fileUri.AbsolutePath), out var id)) unreadable.Add(id);
                    return null;
                }
                return new SyncRemoteObject(SyncEnvelopeCodec.Deserialize(bytes), bytes);
            }
            catch (FormatException)
            {
                // Un archivo incompleto o de otra versión no debe impedir el resto de la sincronización.
                return null;
            }
            catch (JsonException)
            {
                // Igual que arriba: una escritura interrumpida no invalida todo el almacén.
                return null;
            }
        });
    }

    /// <summary>El contenido entero, o null si pasa del tope de respuesta (se lee por trozos: no hace falta que venga Content-Length).</summary>
    private static async Task<byte[]?> ReadCappedAsync(HttpContent content, CancellationToken cancellation)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellation).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > HttpSyncTransport.MaxResponseBytes) return null;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    public void Write(SyncEnvelope envelope)
    {
        EnsureCollection();
        using var content = new ByteArrayContent(SyncEnvelopeCodec.Serialize(envelope));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = _client.PutAsync(new Uri(_objectsUri, $"{envelope.NoteId:N}.json"), content)
            .GetAwaiter().GetResult();
        EnsureSuccess(response);
    }

    public void Delete(Guid noteId)
    {
        EnsureCollection();
        using var response = _client.DeleteAsync(new Uri(_objectsUri, $"{noteId:N}.json"))
            .GetAwaiter().GetResult();
        if (response.StatusCode != HttpStatusCode.NotFound) EnsureSuccess(response);
    }

    private void EnsureCollection()
    {
        if (_collectionReady) return;

        using var request = new HttpRequestMessage(new HttpMethod("MKCOL"), _objectsUri);
        using var response = _client.SendAsync(request).GetAwaiter().GetResult();
        if (response.StatusCode != HttpStatusCode.Created && response.StatusCode != HttpStatusCode.MethodNotAllowed)
            EnsureSuccess(response);

        _collectionReady = true;
    }

    /// <summary>
    /// La dirección de un archivo del listado, siempre en el servidor configurado: de un href absoluto solo se usa la ruta
    /// (un servidor tras un proxy puede devolver su dirección interna, con otro host o puerto) y, si aun así el resultado
    /// cae en otro host o esquema (un href «//otro/x»), se descarta. Devuelve null cuando no hay que seguirlo.
    /// </summary>
    internal static Uri? ResolveHref(Uri objectsUri, string href)
    {
        var target = Uri.TryCreate(href, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https"
            ? absolute.PathAndQuery
            : href;
        if (!Uri.TryCreate(objectsUri, target, out var resolved)) return null;
        var sameServer = resolved.Scheme == objectsUri.Scheme && string.Equals(resolved.Host, objectsUri.Host, StringComparison.OrdinalIgnoreCase)
            && resolved.Port == objectsUri.Port;
        return sameServer ? resolved : null;
    }

    private static string NormalizeBaseUrl(string value)
    {
        var trimmed = value.Trim();
        return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : $"{trimmed}/";
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("WebDAV rechazó las credenciales (401). Revisa el usuario y la contraseña de aplicación.");
        response.EnsureSuccessStatusCode();
    }
}
