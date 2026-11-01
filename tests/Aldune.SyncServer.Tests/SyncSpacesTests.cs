using Aldune.SyncServer;

namespace Aldune.SyncServer.Tests;

/// <summary>`ALDUNE_SYNC_SPACES`: one server, one token per person, each token with its own store.</summary>
public sealed class SyncSpacesTests
{
    private const string A = "token-of-the-first-space-long-enough";
    private const string B = "token-of-the-second-space-long-enough";
    private const string Legacy = "the-original-token-of-this-server-long";

    [Fact]
    public void Parse_ReadsNameAndTokenPairsSeparatedByCommasOrLines()
    {
        var spaces = SyncSpaces.Parse($"madre={A}, familia={B}\nyo=another-token");

        Assert.Equal(new[] { "madre", "familia", "yo" }, spaces.Select(s => s.Name));
        Assert.Equal(A, spaces[0].Token);
    }

    [Fact]
    public void Parse_NothingConfigured_IsEmpty()
    {
        Assert.Empty(SyncSpaces.Parse(null));
        Assert.Empty(SyncSpaces.Parse("  "));
    }

    [Theory]
    [InlineData("sin-igual")]              // no «=»
    [InlineData("=token")]                 // no name
    [InlineData("madre=")]                 // no token
    [InlineData("Mi Madre=token")]         // a name is a folder: lowercase letters, digits, - and _
    [InlineData("../madre=token")]         // never a path
    [InlineData("a=1,a=2")]                // the same name twice
    [InlineData("a=same,b=same")]          // the same token in two spaces would mix them
    public void Parse_RejectsWhatCouldMixOrEscapeSpaces(string spec)
    {
        Assert.Throws<FormatException>(() => SyncSpaces.Parse(spec));
    }

    [Fact]
    public void Parse_AnOverlongNameIsRejected()
    {
        Assert.Throws<FormatException>(() => SyncSpaces.Parse(new string('a', 33) + "=token"));
    }

    [Fact]
    public void Resolve_FindsTheSpaceOfEachToken_AndTheLegacyOneIsTheRoot()
    {
        var spaces = SyncSpaces.Parse($"madre={A},familia={B}");
        var legacy = new[] { Legacy };

        Assert.Equal("madre", SyncSpaces.Resolve($"Bearer {A}", legacy, spaces)!.Value.Name);
        Assert.Equal("familia", SyncSpaces.Resolve($"Bearer {B}", legacy, spaces)!.Value.Name);
        Assert.Null(SyncSpaces.Resolve($"Bearer {Legacy}", legacy, spaces)!.Value.Name);
    }

    [Fact]
    public void Resolve_AWrongOrMissingToken_IsNull()
    {
        var spaces = SyncSpaces.Parse($"madre={A}");

        Assert.Null(SyncSpaces.Resolve($"Bearer {A}x", new[] { Legacy }, spaces));
        Assert.Null(SyncSpaces.Resolve(null, new[] { Legacy }, spaces));
        Assert.Null(SyncSpaces.Resolve("Basic abc", new[] { Legacy }, spaces));
    }

    [Fact]
    public void EnsureDistinct_ALegacyTokenThatIsAlsoASpaceToken_IsRejected()
    {
        var spaces = SyncSpaces.Parse($"madre={A}");

        Assert.Throws<FormatException>(() => SyncSpaces.EnsureDistinct(new[] { A }, spaces));
        SyncSpaces.EnsureDistinct(new[] { Legacy }, spaces);
    }
}
