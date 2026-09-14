namespace Aldune.Core.Tests;

public sealed class SyncTokenTests
{
    [Fact]
    public void Parse_PrefersRotatingListAndRemovesDuplicates()
    {
        var tokens = SyncTokenSet.Parse(" new-token, old-token, new-token ", "legacy-token");

        Assert.Equal(new[] { "new-token", "old-token" }, tokens);
    }

    [Fact]
    public void Parse_FallsBackToLegacyToken()
    {
        var tokens = SyncTokenSet.Parse("", "legacy-token");

        Assert.Equal(new[] { "legacy-token" }, tokens);
    }

    [Theory]
    [InlineData("Bearer new-token", true)]
    [InlineData("bearer old-token", true)]
    [InlineData("Bearer revoked-token", false)]
    [InlineData("Basic new-token", false)]
    [InlineData("", false)]
    public void Matches_AcceptsOnlyActiveBearerTokens(string authorization, bool expected)
    {
        var tokens = SyncTokenSet.Parse("new-token,old-token", null);

        Assert.Equal(expected, SyncTokenSet.Matches(authorization, tokens));
    }
}
