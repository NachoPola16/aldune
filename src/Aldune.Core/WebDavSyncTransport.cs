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

    public WebDavSyncTransport(string serverUrl, string username, string password)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(serverUrl), UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme is not "http" and not "https"))
            throw new ArgumentException("The WebDAV URL must be an absolute HTTP(S) URL.", nameof(serverUrl));
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("A WebDAV username is required.", nameof(username));
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("A WebDAV password is required.", nameof(password));

        _objectsUri = new Uri(baseUri, "objects/");
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    public IReadOnlyList<SyncRemoteObject> ReadAll()
    {
        EnsureCollection();
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
        var result = new List<SyncRemoteObject>();

        foreach (var resource in document.Descendants(Dav + "response"))
        {
            var href = resource.Element(Dav + "href")?.Value;
            if (string.IsNullOrWhiteSpace(href) || resource.Descendants(Dav + "collection").Any()) continue;

            var fileUri = ResolveHref(href);
            if (!fileUri.AbsolutePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                using var fileResponse = _client.GetAsync(fileUri).GetAwaiter().GetResult();
                EnsureSuccess(fileResponse);
                var bytes = fileResponse.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                result.Add(new SyncRemoteObject(SyncEnvelopeCodec.Deserialize(bytes), bytes));
            }
            catch (FormatException)
            {
                // Un archivo incompleto o de otra versión no debe impedir el resto de la sincronización.
            }
            catch (JsonException)
            {
                // Igual que arriba: una escritura interrumpida no invalida todo el almacén.
            }
        }

        return result;
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

    private Uri ResolveHref(string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var absolute)) return absolute;
        return new Uri(_objectsUri, href);
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
