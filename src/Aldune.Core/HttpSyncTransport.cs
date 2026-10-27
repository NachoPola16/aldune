using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Aldune.Core;

/// <summary>
/// Transporte contra el servidor autohosteable. El servidor solo enumera y almacena sobres cifrados;
/// no recibe la clave de sincronización.
/// </summary>
public sealed class HttpSyncTransport : ISyncTransport
{
    /// <summary>Lo que se espera a un servidor que no contesta, aquí y en WebDAV: corto para no dejar la sync
    /// colgada al salir de casa sin VPN, largo para una subida lenta.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    private readonly HttpClient _client;
    private readonly Uri _baseUri;

    public HttpSyncTransport(string serverUrl, string token, TimeSpan? timeout = null)
    {
        if (!System.Uri.TryCreate(serverUrl, UriKind.Absolute, out _baseUri!) ||
            (_baseUri.Scheme is not "http" and not "https"))
            throw new ArgumentException("The sync server URL must be an absolute HTTP(S) URL.", nameof(serverUrl));
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("A sync server token is required.", nameof(token));

        _client = new HttpClient { Timeout = timeout ?? DefaultTimeout };
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    internal TimeSpan Timeout => _client.Timeout;

    public IReadOnlyList<SyncRemoteObject> ReadAll()
    {
        using var listResponse = _client.GetAsync(BuildUri("api/v1/objects")).GetAwaiter().GetResult();
        EnsureSuccess(listResponse);
        var descriptors = listResponse.Content.ReadFromJsonAsync<List<SyncObjectDescriptor>>()
            .GetAwaiter().GetResult() ?? new List<SyncObjectDescriptor>();

        return ParallelDownload.Run(descriptors, async descriptor =>
        {
            using var response = await _client.GetAsync(BuildUri($"api/v1/objects/{descriptor.NoteId:N}")).ConfigureAwait(false);
            EnsureSuccess(response);
            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            return new SyncRemoteObject(SyncEnvelopeCodec.Deserialize(bytes), bytes);
        });
    }

    public void Write(SyncEnvelope envelope)
    {
        using var content = new ByteArrayContent(SyncEnvelopeCodec.Serialize(envelope));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = _client.PutAsync(BuildUri($"api/v1/objects/{envelope.NoteId:N}"), content)
            .GetAwaiter().GetResult();
        EnsureSuccess(response);
    }

    public void Delete(Guid noteId)
    {
        using var response = _client.DeleteAsync(BuildUri($"api/v1/objects/{noteId:N}"))
            .GetAwaiter().GetResult();
        EnsureSuccess(response);
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new InvalidOperationException(
                "The sync server rejected the token (401). Paste only the value after ALDUNE_SYNC_TOKEN=.");
        response.EnsureSuccessStatusCode();
    }

    private System.Uri BuildUri(string relative) => new(_baseUri, relative);

    private sealed class SyncObjectDescriptor
    {
        public Guid NoteId { get; set; }
        public bool Tombstone { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }
}
