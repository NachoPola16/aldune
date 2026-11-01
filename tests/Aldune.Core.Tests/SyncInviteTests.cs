using Aldune.Core;

namespace Aldune.Core.Tests;

public sealed class SyncInviteTests
{
    [Fact]
    public void FormatThenParseRoundTrips()
    {
        var text = SyncInvite.Format("https://aldune.example.com/", "abc_DEF-123");

        Assert.True(SyncInvite.TryParse(text, out var url, out var code));
        Assert.Equal("https://aldune.example.com/", url);
        Assert.Equal("abc_DEF-123", code);
    }

    [Fact]
    public void ParseToleratesWhatAChatAdds()
    {
        var text = SyncInvite.Format("https://aldune.example.com/", "the-code");
        var wrapped = "  " + text[..20] + "\r\n " + text[20..] + "  ";

        Assert.True(SyncInvite.TryParse(wrapped, out _, out var code));
        Assert.Equal("the-code", code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("aldune-invite:")]
    [InlineData("aldune-invite:!!!not-base64!!!")]
    [InlineData("aldune-invite:e30")] // {} : no url, no code
    public void ParseRefusesGarbage(string? text)
    {
        Assert.False(SyncInvite.TryParse(text, out _, out _));
    }

    [Theory]
    [InlineData("file:///c:/secret")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/")]
    public void ParseNeverAcceptsAnAddressThatIsNotHttp(string url)
    {
        Assert.False(SyncInvite.TryParse(SyncInvite.Format(url, "code"), out _, out _));
    }

    [Fact]
    public void ParseRefusesAnAbsurdlyLongText()
    {
        Assert.False(SyncInvite.TryParse(SyncInvite.Prefix + new string('A', 10_000), out _, out _));
    }
}
