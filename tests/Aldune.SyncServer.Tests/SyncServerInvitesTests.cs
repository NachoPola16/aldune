using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aldune.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aldune.SyncServer.Tests;

/// <summary>Invitations over HTTP: the owner (admin token) makes a code, a guest redeems it and gets a space of their own.</summary>
[Collection(nameof(SyncServerCollection))]
public sealed class SyncServerInvitesTests : IDisposable
{
    private const string Admin = "the-admin-token-of-this-server-long-enough";
    private const string Legacy = "the-original-token-of-this-server-long";
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"aldune-invites-{Guid.NewGuid():N}");
    private WebApplicationFactory<Program>? _factory;

    private void Start(string? admin = Admin, string? maxSpaces = null)
    {
        Environment.SetEnvironmentVariable(BrandIdentity.SyncDataDirEnvVar, _dataDir);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncTokensEnvVar, null);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncTokenEnvVar, Legacy);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncSpacesEnvVar, null);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncAdminTokenEnvVar, admin);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncMaxSpacesEnvVar, maxSpaces);
        _factory = new WebApplicationFactory<Program>();
    }

    private HttpClient Client(string? bearer = null)
    {
        var client = _factory!.CreateClient();
        if (bearer is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return client;
    }

    private async Task<string> NewCode(string label = "hermano")
    {
        using var admin = Client(Admin);
        using var response = await admin.PostAsJsonAsync("/admin/v1/invites", new { label });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
    }

    private async Task<(HttpStatusCode Status, string? Token)> Redeem(string code)
    {
        using var guest = Client();
        using var response = await guest.PostAsJsonAsync("/invites/v1/redeem", new { code });
        var token = response.IsSuccessStatusCode ? (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString() : null;
        return (response.StatusCode, token);
    }

    public void Dispose()
    {
        _factory?.Dispose();
        Environment.SetEnvironmentVariable(BrandIdentity.SyncAdminTokenEnvVar, null);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncMaxSpacesEnvVar, null);
        if (Directory.Exists(_dataDir)) Directory.Delete(_dataDir, recursive: true);
    }

    [Fact]
    public async Task WithoutAnAdminTokenThereAreNoInvitationRoutes()
    {
        Start(admin: null);
        using var client = Client();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/invites/v1/redeem", new { code = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/admin/v1/invites", new { })).StatusCode);
    }

    [Fact]
    public async Task OnlyTheAdminTokenCreatesInvitations()
    {
        Start();
        using var stranger = Client("not-the-admin-token");
        using var store = Client(Legacy);

        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.PostAsJsonAsync("/admin/v1/invites", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await store.PostAsJsonAsync("/admin/v1/invites", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().PostAsJsonAsync("/admin/v1/invites", new { })).StatusCode);
    }

    [Fact]
    public async Task AGuestRedeemsACodeAndGetsAnIsolatedSpace()
    {
        Start();
        var (status, token) = await Redeem(await NewCode());
        Assert.Equal(HttpStatusCode.OK, status);
        using var guest = Client(token);
        using var owner = Client(Legacy);
        var id = Guid.NewGuid();

        using var put = await guest.PutAsync($"/api/v1/objects/{id}", Json(Envelope(id)));

        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"/api/v1/objects/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/v1/objects/{id}")).StatusCode);
        Assert.Equal("[]", await (await owner.GetAsync("/api/v1/objects")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ACodeWorksOnceAndAWrongOneIs400()
    {
        Start();
        var code = await NewCode();

        Assert.Equal(HttpStatusCode.OK, (await Redeem(code)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(code)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem("not-a-code")).Status);
    }

    [Fact]
    public async Task TheAdminTokenCannotReadAnyStore()
    {
        Start();
        using var admin = Client(Admin);

        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/v1/objects")).StatusCode);
    }

    [Fact]
    public async Task RevokingASpaceCutsItsTokenButKeepsItsFiles()
    {
        Start();
        var (_, token) = await Redeem(await NewCode());
        using var guest = Client(token);
        var id = Guid.NewGuid();
        await guest.PutAsync($"/api/v1/objects/{id}", Json(Envelope(id)));
        using var admin = Client(Admin);
        var spaces = await (await admin.GetAsync("/admin/v1/spaces")).Content.ReadFromJsonAsync<JsonElement>();
        var name = spaces[0].GetProperty("name").GetString()!;

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/admin/v1/spaces/{name}")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/v1/objects")).StatusCode);
        Assert.True(File.Exists(Path.Combine(_dataDir, "spaces", name, "objects", $"{id:N}.json")));
    }

    [Fact]
    public async Task TheSpaceLimitStopsFurtherInvitations()
    {
        Start(maxSpaces: "1");

        Assert.Equal(HttpStatusCode.OK, (await Redeem(await NewCode())).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Redeem(await NewCode())).Status);
    }

    [Fact]
    public void AnAdminTokenEqualToAStoreTokenRefusesToStart()
    {
        Start(admin: Legacy);

        Assert.ThrowsAny<Exception>(() => _factory!.CreateClient());
    }

    [Fact]
    public async Task AGuessingGuestIsThrottled()
    {
        Start();
        HttpStatusCode last = default;
        for (var i = 0; i < 15; i++) last = (await Redeem("guess-" + i)).Status;

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
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
}
