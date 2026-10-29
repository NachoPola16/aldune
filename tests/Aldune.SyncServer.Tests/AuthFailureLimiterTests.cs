using System.Net;
using Aldune.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aldune.SyncServer.Tests;

public sealed class AuthFailureLimiterTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UnderTheLimit_NobodyIsBlocked()
    {
        var limiter = new AuthFailureLimiter(maxFailures: 3, TimeSpan.FromMinutes(1));

        for (var i = 0; i < 2; i++) limiter.RecordFailure("1.1.1.1", T0);

        Assert.Null(limiter.RetryAfter("1.1.1.1", T0));
    }

    [Fact]
    public void AtTheLimit_TheAddressIsBlockedUntilTheOldestFailureLeavesTheWindow()
    {
        var limiter = new AuthFailureLimiter(3, TimeSpan.FromMinutes(1));
        limiter.RecordFailure("1.1.1.1", T0);
        limiter.RecordFailure("1.1.1.1", T0.AddSeconds(10));
        limiter.RecordFailure("1.1.1.1", T0.AddSeconds(20));

        var wait = limiter.RetryAfter("1.1.1.1", T0.AddSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(30), wait);
        Assert.Null(limiter.RetryAfter("1.1.1.1", T0.AddSeconds(61)));
    }

    [Fact]
    public void OtherAddressesAreNotAffected()
    {
        var limiter = new AuthFailureLimiter(2, TimeSpan.FromMinutes(1));
        limiter.RecordFailure("1.1.1.1", T0);
        limiter.RecordFailure("1.1.1.1", T0);

        Assert.NotNull(limiter.RetryAfter("1.1.1.1", T0));
        Assert.Null(limiter.RetryAfter("2.2.2.2", T0));
    }

    [Fact]
    public void ZeroMeansDisabled()
    {
        var limiter = new AuthFailureLimiter(0, TimeSpan.FromMinutes(1));
        for (var i = 0; i < 100; i++) limiter.RecordFailure("1.1.1.1", T0);

        Assert.Null(limiter.RetryAfter("1.1.1.1", T0));
    }

    [Fact]
    public void OnlyTimeReleasesAnAddress_NotAGoodToken()
    {
        var limiter = new AuthFailureLimiter(2, TimeSpan.FromMinutes(1));
        limiter.RecordFailure("1.1.1.1", T0);
        limiter.RecordFailure("1.1.1.1", T0);

        // No hay RecordSuccess: acertar no borra los fallos, o se podría adivinar intercalando un token bueno conocido.
        Assert.NotNull(limiter.RetryAfter("1.1.1.1", T0.AddSeconds(5)));
    }

    [Fact]
    public void TheMemoryIsBounded()
    {
        var limiter = new AuthFailureLimiter(5, TimeSpan.FromMinutes(1), maxTrackedAddresses: 100);
        for (var i = 0; i < 1000; i++) limiter.RecordFailure($"10.0.{i / 256}.{i % 256}", T0);

        Assert.True(limiter.TrackedAddresses <= 100);
    }

    [Theory]
    [InlineData("203.0.113.9", "10.0.0.1", true, "203.0.113.9")]
    [InlineData("198.51.100.1, 203.0.113.9", "10.0.0.1", true, "203.0.113.9")]
    [InlineData("203.0.113.9", "10.0.0.1", false, "10.0.0.1")]
    [InlineData(null, "10.0.0.1", true, "10.0.0.1")]
    [InlineData("no-es-una-ip", "10.0.0.1", true, "10.0.0.1")]
    public void ClientAddress_UsesTheLastForwardedEntryOnlyWhenTheProxyIsTrusted(string? forwarded, string remote, bool trustProxy, string expected) =>
        Assert.Equal(expected, AuthFailureLimiter.ClientAddress(forwarded, IPAddress.Parse(remote), trustProxy));
}

/// <summary>El límite dentro del servidor real. Las variables de entorno son de todo el proceso: misma colección sin paralelismo.</summary>
[Collection(nameof(SyncServerCollection))]
public sealed class SyncServerRateLimitTests : IDisposable
{
    private const string Token = "a-token-that-is-long-enough-for-the-tests";
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"aldune-ratelimit-{Guid.NewGuid():N}");
    private WebApplicationFactory<Program>? _factory;

    private HttpClient Start(string? max, string? trustProxy)
    {
        Environment.SetEnvironmentVariable(BrandIdentity.SyncDataDirEnvVar, _dataDir);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncTokensEnvVar, null);
        Environment.SetEnvironmentVariable(BrandIdentity.SyncTokenEnvVar, Token);
        Environment.SetEnvironmentVariable(AuthFailureLimiter.MaxFailuresEnvVar, max);
        Environment.SetEnvironmentVariable(AuthFailureLimiter.TrustProxyEnvVar, trustProxy);
        _factory = new WebApplicationFactory<Program>();
        return _factory.CreateClient();
    }

    public void Dispose()
    {
        _factory?.Dispose();
        Environment.SetEnvironmentVariable(AuthFailureLimiter.MaxFailuresEnvVar, null);
        Environment.SetEnvironmentVariable(AuthFailureLimiter.TrustProxyEnvVar, null);
        try { Directory.Delete(_dataDir, true); } catch (IOException) { }
    }

    private static HttpRequestMessage Get(string? token, string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/objects");
        if (token is not null) request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        if (forwardedFor is not null) request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        return request;
    }

    [Fact]
    public async Task AfterTooManyWrongTokens_EvenTheRightOneGets429WithRetryAfter()
    {
        var client = Start("3", null);
        for (var i = 0; i < 3; i++)
        {
            using var wrong = await client.SendAsync(Get("nope"));
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using var blocked = await client.SendAsync(Get(Token));

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public async Task TheHealthEndpointIsNeverLimited()
    {
        var client = Start("1", null);
        using var wrong = await client.SendAsync(Get("nope"));

        using var health = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task CorrectTokensFromOtherClients_KeepWorkingWhenTheProxyIsTrusted()
    {
        var client = Start("2", "1");
        for (var i = 0; i < 2; i++)
        {
            using var wrong = await client.SendAsync(Get("nope", forwardedFor: "203.0.113.9"));
        }

        using var attacker = await client.SendAsync(Get(Token, forwardedFor: "203.0.113.9"));
        using var other = await client.SendAsync(Get(Token, forwardedFor: "198.51.100.7"));

        Assert.Equal(HttpStatusCode.TooManyRequests, attacker.StatusCode);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Fact]
    public async Task WithoutTheProxySetting_ForwardedForCannotBeUsedToEscapeTheLimit()
    {
        var client = Start("2", null);
        for (var i = 0; i < 2; i++)
        {
            using var wrong = await client.SendAsync(Get("nope", forwardedFor: $"203.0.113.{i}"));
        }

        // Cambiar la cabecera no cambia quién es: sigue contando por la dirección de la conexión.
        using var spoofed = await client.SendAsync(Get(Token, forwardedFor: "198.51.100.200"));

        Assert.Equal(HttpStatusCode.TooManyRequests, spoofed.StatusCode);
    }

    [Fact]
    public async Task ZeroDisablesTheLimit()
    {
        var client = Start("0", null);
        for (var i = 0; i < 30; i++)
        {
            using var wrong = await client.SendAsync(Get("nope"));
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using var right = await client.SendAsync(Get(Token));

        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
    }
}
