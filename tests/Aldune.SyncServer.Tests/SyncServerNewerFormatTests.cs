using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Aldune.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aldune.SyncServer.Tests;

/// <summary>
/// An object stored by a client of a newer sync format must neither vanish from the listing nor be replaced by an older client: the old
/// behaviour treated it as damaged, so the listing hid it and a PUT overwrote it, dropping the fields the old writer does not know.
/// </summary>
[Collection(nameof(SyncServerCollection))]
public sealed class SyncServerNewerFormatTests : IDisposable
{
    private const string Token = "a-token-that-is-long-enough-for-the-tests";
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"aldune-server-newer-{Guid.NewGuid():N}");
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly Guid _id = Guid.NewGuid();
    private readonly string _objectPath;
    private readonly byte[] _newer;

    public SyncServerNewerFormatTests()
    {
        Environment.SetEnvironmentVariable(BrandIdentity.SyncDataDirEnvVar, _dataDir);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncTokensEnvVar, null);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncTokenEnvVar, Token);
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        // As a client of format 5 would have stored it (Serialize does not validate the format).
        var envelope = Envelope(DateTimeOffset.UtcNow.AddMinutes(-5));
        envelope.Format = SyncCompatibility.CurrentFormat + 1;
        _newer = SyncEnvelopeCodec.Serialize(envelope);
        var objects = Path.Combine(_dataDir, "objects");
        Directory.CreateDirectory(objects);
        _objectPath = Path.Combine(objects, $"{_id:N}.json");
        File.WriteAllBytes(_objectPath, _newer);
    }

    private SyncEnvelope Envelope(DateTimeOffset updatedAt) => new()
    {
        NoteId = _id,
        UpdatedAt = updatedAt,
        DeviceId = "device",
        CipherText = Convert.ToBase64String(new byte[64]),
        Nonce = Convert.ToBase64String(new byte[12]),
        Tag = Convert.ToBase64String(new byte[16])
    };

    [Fact]
    public async Task TheListingIncludesAnObjectOfANewerFormatAndSaysWhichFormat()
    {
        using var response = await _client.GetAsync("/api/v1/objects");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(_id, item.GetProperty("noteId").GetGuid());
        Assert.Equal(SyncCompatibility.CurrentFormat + 1, item.GetProperty("format").GetInt32());
    }

    [Fact]
    public async Task APutNeverReplacesAnObjectOfANewerFormat_EvenWithAMoreRecentDate()
    {
        var content = new ByteArrayContent(SyncEnvelopeCodec.Serialize(Envelope(DateTimeOffset.UtcNow)));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await _client.PutAsync($"/api/v1/objects/{_id}", content);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("newer Aldune", await response.Content.ReadAsStringAsync());
        Assert.Equal(_newer, File.ReadAllBytes(_objectPath));
    }

    [Fact]
    public async Task ADamagedObjectIsStillListedOutAndReplaceable()
    {
        File.WriteAllText(_objectPath, "{ not json");

        using var list = await _client.GetAsync("/api/v1/objects");
        var content = new ByteArrayContent(SyncEnvelopeCodec.Serialize(Envelope(DateTimeOffset.UtcNow)));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var put = await _client.PutAsync($"/api/v1/objects/{_id}", content);

        Assert.Equal("[]", (await list.Content.ReadAsStringAsync()).Trim());
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        if (Directory.Exists(_dataDir)) Directory.Delete(_dataDir, recursive: true);
    }
}
