namespace Aldune.SyncServer.Tests;

/// <summary>A clock the test moves by hand.</summary>
internal sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>The registry of invitations and of the spaces created from them (the part of the server that is not in the environment).</summary>
public sealed class SyncRegistryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-registry-{Guid.NewGuid():N}");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));

    private SyncRegistry Create(int maxSpaces = 20)
    {
        Directory.CreateDirectory(_dir);
        return new SyncRegistry(Path.Combine(_dir, "registry.json"), _clock, maxSpaces);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void AnInviteCreatesASpaceWhoseTokenResolvesToIt()
    {
        var registry = Create();
        var code = registry.CreateInvite("hermano", TimeSpan.FromDays(7)).Code;

        var result = registry.Redeem(code);

        Assert.Equal(RedeemStatus.Ok, result.Status);
        Assert.Matches("^u-[0-9a-f]{8}$", result.Space!);
        Assert.True(result.Token!.Length >= 40);
        Assert.Equal(result.Space, registry.Resolve("Bearer " + result.Token));
        Assert.Null(registry.Resolve("Bearer " + result.Token + "x"));
        Assert.Null(registry.Resolve(null));
    }

    [Fact]
    public void ACodeWorksOnlyOnce()
    {
        var registry = Create();
        var code = registry.CreateInvite(null, TimeSpan.FromDays(7)).Code;

        Assert.Equal(RedeemStatus.Ok, registry.Redeem(code).Status);
        Assert.Equal(RedeemStatus.Invalid, registry.Redeem(code).Status);
    }

    [Fact]
    public void AnExpiredCodeIsRefused()
    {
        var registry = Create();
        var code = registry.CreateInvite(null, TimeSpan.FromDays(1)).Code;

        _clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1));

        Assert.Equal(RedeemStatus.Invalid, registry.Redeem(code).Status);
        Assert.Empty(registry.Spaces);
    }

    [Fact]
    public void AGuessedCodeIsRefused()
    {
        var registry = Create();
        registry.CreateInvite(null, TimeSpan.FromDays(1));

        Assert.Equal(RedeemStatus.Invalid, registry.Redeem("not-a-real-code").Status);
        Assert.Equal(RedeemStatus.Invalid, registry.Redeem("").Status);
    }

    [Fact]
    public void TheServerNeverStoresTheCodeOrTheTokenInClear()
    {
        var registry = Create();
        var code = registry.CreateInvite("x", TimeSpan.FromDays(1)).Code;
        var token = registry.Redeem(code).Token!;

        var file = File.ReadAllText(Path.Combine(_dir, "registry.json"));

        Assert.DoesNotContain(code, file);
        Assert.DoesNotContain(token, file);
    }

    [Fact]
    public void TheRegistrySurvivesARestart()
    {
        var first = Create();
        var token = first.Redeem(first.CreateInvite("a", TimeSpan.FromDays(1)).Code).Token!;
        var pending = first.CreateInvite("b", TimeSpan.FromDays(1)).Code;

        var second = Create();

        Assert.NotNull(second.Resolve("Bearer " + token));
        Assert.Equal(RedeemStatus.Ok, second.Redeem(pending).Status);
    }

    [Fact]
    public void RevokingASpaceCutsItsTokenAndKeepsTheOthers()
    {
        var registry = Create();
        var a = registry.Redeem(registry.CreateInvite("a", TimeSpan.FromDays(1)).Code);
        var b = registry.Redeem(registry.CreateInvite("b", TimeSpan.FromDays(1)).Code);

        Assert.True(registry.Revoke(a.Space!));

        Assert.Null(registry.Resolve("Bearer " + a.Token));
        Assert.Equal(b.Space, registry.Resolve("Bearer " + b.Token));
        Assert.False(registry.Revoke(a.Space!));
    }

    [Fact]
    public void ThereIsALimitOfSpaces()
    {
        var registry = Create(maxSpaces: 1);
        Assert.Equal(RedeemStatus.Ok, registry.Redeem(registry.CreateInvite(null, TimeSpan.FromDays(1)).Code).Status);

        var code = registry.CreateInvite(null, TimeSpan.FromDays(1)).Code;

        Assert.Equal(RedeemStatus.Full, registry.Redeem(code).Status);
    }

    [Fact]
    public void TheInviteTimeToLiveIsCapped()
    {
        var registry = Create();

        var invite = registry.CreateInvite(null, TimeSpan.FromDays(3650));

        Assert.True(invite.ExpiresAt <= _clock.GetUtcNow().AddDays(30));
    }
}
