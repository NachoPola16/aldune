using System.Net;
using System.Net.Http.Headers;
using Aldune.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aldune.SyncServer.Tests;

/// <summary>
/// One URL, several stores: each token in `ALDUNE_SYNC_SPACES` sees only its own notes. Same collection as the other server tests
/// (the settings are process-wide environment variables).
/// </summary>
[Collection(nameof(SyncServerCollection))]
public sealed class SyncServerSpacesTests : IDisposable
{
    private const string Madre = "token-of-the-mother-long-enough-for-tests";
    private const string Familia = "token-of-the-family-long-enough-for-tests";
    private const string Legacy = "the-original-token-of-this-server-long";
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"aldune-spaces-{Guid.NewGuid():N}");
    private WebApplicationFactory<Program>? _factory;

    private HttpClient Start(string? legacy, string spaces)
    {
        Environment.SetEnvironmentVariable(BrandIdentity.SyncDataDirEnvVar, _dataDir);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncTokensEnvVar, null);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncTokenEnvVar, legacy);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncSpacesEnvVar, spaces);
        _factory = new WebApplicationFactory<Program>();
        return _factory.CreateClient();
    }

    private HttpClient As(string token)
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task EachTokenSeesOnlyItsOwnNotes()
    {
        using var anonymous = Start(Legacy, $"madre={Madre},familia={Familia}");
        using var madre = As(Madre);
        using var familia = As(Familia);
        using var root = As(Legacy);
        var id = Guid.NewGuid();

        using var put = await madre.PutAsync($"/api/v1/objects/{id}", Json(Envelope(id)));

        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await madre.GetAsync($"/api/v1/objects/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await familia.GetAsync($"/api/v1/objects/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await root.GetAsync($"/api/v1/objects/{id}")).StatusCode);
        Assert.Contains(id.ToString(), await (await madre.GetAsync("/api/v1/objects")).Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("[]", await (await familia.GetAsync("/api/v1/objects")).Content.ReadAsStringAsync());
        Assert.Equal("[]", await (await root.GetAsync("/api/v1/objects")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ADeleteInOneSpaceNeverTouchesAnother()
    {
        using var anonymous = Start(Legacy, $"madre={Madre},familia={Familia}");
        using var madre = As(Madre);
        using var familia = As(Familia);
        var id = Guid.NewGuid();
        await madre.PutAsync($"/api/v1/objects/{id}", Json(Envelope(id)));
        await familia.PutAsync($"/api/v1/objects/{id}", Json(Envelope(id)));

        await familia.DeleteAsync($"/api/v1/objects/{id}");

        Assert.Equal(HttpStatusCode.OK, (await madre.GetAsync($"/api/v1/objects/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await familia.GetAsync($"/api/v1/objects/{id}")).StatusCode);
    }

    [Fact]
    public async Task TheLegacyTokenKeepsTheOriginalStoreAndASpaceLivesInItsOwnFolder()
    {
        using var anonymous = Start(Legacy, $"madre={Madre}");
        using var root = As(Legacy);
        using var madre = As(Madre);
        var rootId = Guid.NewGuid();
        var madreId = Guid.NewGuid();

        await root.PutAsync($"/api/v1/objects/{rootId}", Json(Envelope(rootId)));
        await madre.PutAsync($"/api/v1/objects/{madreId}", Json(Envelope(madreId)));

        Assert.True(File.Exists(Path.Combine(_dataDir, "objects", $"{rootId:N}.json")));
        Assert.True(File.Exists(Path.Combine(_dataDir, "spaces", "madre", "objects", $"{madreId:N}.json")));
    }

    [Fact]
    public async Task AServerWithOnlySpacesStartsAndAWrongTokenIs401()
    {
        using var anonymous = Start(null, $"madre={Madre}");
        using var stranger = As(Madre + "x");
        using var madre = As(Madre);

        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/api/v1/objects")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/objects")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await madre.GetAsync("/api/v1/objects")).StatusCode);
    }

    [Fact]
    public void AConfigurationThatCouldMixSpacesRefusesToStart()
    {
        Environment.SetEnvironmentVariable(BrandIdentity.SyncDataDirEnvVar, _dataDir);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncTokensEnvVar, null);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncTokenEnvVar, Madre);       // the root token equals a space token
        Environment.SetEnvironmentVariable(BrandIdentity.SyncSpacesEnvVar, $"madre={Madre}");
        _factory = new WebApplicationFactory<Program>();

        Assert.ThrowsAny<Exception>(() => _factory.CreateClient());
    }

    private static SyncEnvelope Envelope(Guid id) => new()
    {
        NoteId = id,
        UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        DeviceId = "device",
        CipherText = Convert.ToBase64String(new byte[64]),
        Nonce = Convert.ToBase64String(new byte[12]),
        Tag = Convert.ToBase64String(new byte[16])
    };

    private static ByteArrayContent Json(SyncEnvelope envelope)
    {
        var content = new ByteArrayContent(SyncEnvelopeCodec.Serialize(envelope));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    public void Dispose()
    {
        _factory?.Dispose();
        Environment.SetEnvironmentVariable(BrandIdentity.SyncSpacesEnvVar, null);
        if (Directory.Exists(_dataDir)) Directory.Delete(_dataDir, recursive: true);
    }
}
