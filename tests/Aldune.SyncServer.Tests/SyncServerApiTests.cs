using System.Net;
using System.Net.Http.Headers;
using Aldune.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aldune.SyncServer.Tests;

/// <summary>
/// El servidor lee el token y la carpeta de datos de variables de entorno al arrancar, y las variables son de todo
/// el proceso: por eso estos tests van en una colección sin paralelismo y cada uno arranca su propio servidor sobre
/// una carpeta temporal.
/// </summary>
[CollectionDefinition(nameof(SyncServerCollection), DisableParallelization = true)]
public sealed class SyncServerCollection;

[Collection(nameof(SyncServerCollection))]
public sealed class SyncServerApiTests : IDisposable
{
    private const string Token = "a-token-that-is-long-enough-for-the-tests";
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"aldune-server-{Guid.NewGuid():N}");
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SyncServerApiTests()
    {
        Environment.SetEnvironmentVariable(BrandIdentity.SyncDataDirEnvVar, _dataDir);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncTokensEnvVar, null);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncTokenEnvVar, Token);
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
    }

    [Fact]
    public async Task WithoutAToken_TheApiAnswers401()
    {
        using var anonymous = _factory.CreateClient();

        using var list = await anonymous.GetAsync("/api/v1/objects");
        using var put = await anonymous.PutAsync($"/api/v1/objects/{Guid.NewGuid()}", Json(Envelope(Guid.NewGuid(), 1)));

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
    }

    [Fact]
    public async Task WithAWrongToken_TheApiAnswers401()
    {
        using var stranger = _factory.CreateClient();
        stranger.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token + "x");

        using var response = await stranger.GetAsync("/api/v1/objects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_DoesNotNeedAToken()
    {
        using var anonymous = _factory.CreateClient();

        using var response = await anonymous.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnObjectLargerThanTheLimit_IsRejectedWith413AndNotStored()
    {
        var id = Guid.NewGuid();
        using var content = new ByteArrayContent(new byte[5_000_001]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await _client.PutAsync($"/api/v1/objects/{id}", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Null(await ReadVersion(id));
    }

    [Fact]
    public async Task AnIdThatDoesNotMatchTheUrl_IsRejectedWith400AndNotStored()
    {
        var urlId = Guid.NewGuid();

        using var response = await _client.PutAsync($"/api/v1/objects/{urlId}", Json(Envelope(Guid.NewGuid(), 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await ReadVersion(urlId));
    }

    [Fact]
    public async Task MalformedJson_IsA400NotA500()
    {
        using var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        using var response = await _client.PutAsync($"/api/v1/objects/{Guid.NewGuid()}", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnOlderVersion_DoesNotOverwriteANewerOne()
    {
        var id = Guid.NewGuid();
        using var newer = await _client.PutAsync($"/api/v1/objects/{id}", Json(Envelope(id, 2)));
        using var older = await _client.PutAsync($"/api/v1/objects/{id}", Json(Envelope(id, 1)));

        // El servidor responde igual (204) para no dar pistas a un cliente antiguo; lo que cuenta es lo guardado.
        Assert.Equal(HttpStatusCode.NoContent, newer.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, older.StatusCode);
        Assert.Equal(At(2), await ReadVersion(id));
    }

    [Fact]
    public async Task ANewerVersion_ReplacesAnOlderOne()
    {
        var id = Guid.NewGuid();
        using var older = await _client.PutAsync($"/api/v1/objects/{id}", Json(Envelope(id, 1)));
        using var newer = await _client.PutAsync($"/api/v1/objects/{id}", Json(Envelope(id, 2)));

        Assert.Equal(At(2), await ReadVersion(id));
    }

    [Fact]
    public async Task SimultaneousPutsOfTheSameNote_AlwaysLeaveTheNewestVersion()
    {
        // Una carrera no falla en cada intento: se repite con muchas subidas a la vez y en orden aleatorio, que
        // es lo que hacía aparecer la versión vieja encima de la nueva sin el semáforo.
        var random = new Random(1234);
        for (int round = 0; round < 40; round++)
        {
            var id = Guid.NewGuid();
            var versions = Enumerable.Range(1, 12).OrderBy(_ => random.Next()).ToArray();
            using var start = new ManualResetEventSlim();
            var puts = versions.Select(version => Task.Run(async () =>
            {
                start.Wait();
                using var response = await _client.PutAsync($"/api/v1/objects/{id}", Json(Envelope(id, version)));
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            })).ToArray();
            start.Set();
            await Task.WhenAll(puts);

            Assert.Equal(At(versions.Max()), await ReadVersion(id));
        }
    }

    private static DateTimeOffset At(int version) =>
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(version);

    // El servidor no descifra nada: le basta un sobre con el formato válido, así que el contenido es de relleno.
    private static SyncEnvelope Envelope(Guid id, int version) => new()
    {
        NoteId = id,
        UpdatedAt = At(version),
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

    private async Task<DateTimeOffset?> ReadVersion(Guid id)
    {
        using var response = await _client.GetAsync($"/api/v1/objects/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return SyncEnvelopeCodec.Deserialize(await response.Content.ReadAsByteArrayAsync()).UpdatedAt;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        if (Directory.Exists(_dataDir)) Directory.Delete(_dataDir, recursive: true);
    }
}
