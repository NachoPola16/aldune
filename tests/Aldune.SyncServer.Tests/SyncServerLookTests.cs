using System.Net;
using System.Net.Http.Headers;
using Aldune.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aldune.SyncServer.Tests;

/// <summary>
/// The look document (docs/specs/2026-11-01-sync-look-design.md): one opaque blob per space, stored next to the objects and never among them, so a
/// version that does not know it never lists it.
/// </summary>
[Collection(nameof(SyncServerCollection))]
public sealed class SyncServerLookTests : IDisposable
{
    private const string Madre = "token-of-the-mother-long-enough-for-tests";
    private const string Familia = "token-of-the-family-long-enough-for-tests";
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"aldune-look-{Guid.NewGuid():N}");
    private WebApplicationFactory<Program>? _factory;

    private void Start()
    {
        Environment.SetEnvironmentVariable(BrandIdentity.SyncDataDirEnvVar, _dataDir);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncTokensEnvVar, null);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncTokenEnvVar, null);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncAdminTokenEnvVar, null);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncSpacesEnvVar, $"madre={Madre},familia={Familia}");
        _factory = new WebApplicationFactory<Program>();
    }

    private HttpClient As(string token)
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static ByteArrayContent Blob(byte[] bytes) => new(bytes);

    public void Dispose()
    {
        _factory?.Dispose();
        if (Directory.Exists(_dataDir)) Directory.Delete(_dataDir, recursive: true);
    }

    [Fact]
    public async Task ThereIsNoLookUntilOneIsStored()
    {
        Start();
        using var madre = As(Madre);

        Assert.Equal(HttpStatusCode.NotFound, (await madre.GetAsync("/api/v1/look")).StatusCode);
    }

    [Fact]
    public async Task APutIsReadBackByteForByte()
    {
        Start();
        using var madre = As(Madre);
        var bytes = Enumerable.Range(0, 300).Select(i => (byte)i).ToArray();

        Assert.Equal(HttpStatusCode.NoContent, (await madre.PutAsync("/api/v1/look", Blob(bytes))).StatusCode);

        using var response = await madre.GetAsync("/api/v1/look");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task EachSpaceHasItsOwnLook()
    {
        Start();
        using var madre = As(Madre);
        using var familia = As(Familia);
        await madre.PutAsync("/api/v1/look", Blob([1, 2, 3]));

        Assert.Equal(HttpStatusCode.NotFound, (await familia.GetAsync("/api/v1/look")).StatusCode);
    }

    [Fact]
    public async Task ItNeedsAToken()
    {
        Start();
        using var stranger = As("not-a-token");

        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/api/v1/look")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.PutAsync("/api/v1/look", Blob([1]))).StatusCode);
    }

    [Fact]
    public async Task AnOversizedLookIsRefused()
    {
        Start();
        using var madre = As(Madre);

        using var response = await madre.PutAsync("/api/v1/look", Blob(new byte[64 * 1024 + 1]));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await madre.GetAsync("/api/v1/look")).StatusCode);
    }

    [Fact]
    public async Task TheLookNeverAppearsAmongTheObjects()
    {
        Start();
        using var madre = As(Madre);
        await madre.PutAsync("/api/v1/look", Blob([1, 2, 3]));

        Assert.Equal("[]", await (await madre.GetAsync("/api/v1/objects")).Content.ReadAsStringAsync());
        Assert.False(Directory.EnumerateFiles(Path.Combine(_dataDir, "spaces", "madre", "objects")).Any());
    }
}
