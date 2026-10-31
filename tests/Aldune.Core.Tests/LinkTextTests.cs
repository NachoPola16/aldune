using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class LinkTextTests
{
    [Theory]
    [InlineData("see https://example.com/a?b=1 now", 8, "https://example.com/a?b=1")]
    [InlineData("http://example.com", 0, "http://example.com")]
    [InlineData("see https://example.com.", 10, "https://example.com")]            // sentence dot is not part of it
    [InlineData("(https://example.com/x)", 5, "https://example.com/x")]            // unbalanced closing parenthesis
    [InlineData("https://en.wikipedia.org/wiki/A_(b)", 3, "https://en.wikipedia.org/wiki/A_(b)")] // balanced one stays
    [InlineData("go to https://example.com, then", 10, "https://example.com")]
    [InlineData("HTTPS://EXAMPLE.COM/Path", 0, "HTTPS://EXAMPLE.COM/Path")]
    public void FindAt_ReturnsTheUrlUnderTheIndex(string text, int index, string expected)
    {
        var span = LinkText.FindAt(text, index);
        Assert.NotNull(span);
        Assert.Equal(expected, text.Substring(span!.Value.Start, span.Value.Length));
    }

    [Theory]
    [InlineData("see https://example.com now", 2)]       // before it
    [InlineData("see https://example.com now", 24)]      // after it
    [InlineData("no links here", 3)]
    [InlineData("", 0)]
    public void FindAt_ReturnsNullOutsideAUrl(string text, int index) => Assert.Null(LinkText.FindAt(text, index));

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/x")]
    [InlineData("data:text/html,hi")]
    [InlineData("\\\\server\\share")]
    [InlineData("mailto:a@b.com")]
    public void FindAt_IgnoresEverythingThatIsNotHttp(string text) => Assert.Null(LinkText.FindAt(text, 3));

    [Fact]
    public void FindAt_IgnoresUserInfoThatDisguisesTheHost() =>
        Assert.Null(LinkText.FindAt("https://good-host.com@attacker.tld/", 10));

    [Fact]
    public void FindAt_IgnoresABareScheme() => Assert.Null(LinkText.FindAt("https://", 3));

    [Fact]
    public void FindAt_StopsAtWhitespaceAndAtTheCheckboxGlyphs()
    {
        var text = "☐ https://example.com/a\nnext line";
        var span = LinkText.FindAt(text, 6);
        Assert.Equal("https://example.com/a", text.Substring(span!.Value.Start, span.Value.Length));
    }

    [Fact]
    public void TryGetUri_AcceptsOnlyAbsoluteHttpAndHttps()
    {
        Assert.True(LinkText.TryGetUri("https://example.com/a", out var uri));
        Assert.Equal("example.com", uri!.Host);
        Assert.False(LinkText.TryGetUri("file:///etc/passwd", out _));
        Assert.False(LinkText.TryGetUri("example.com", out _));
        Assert.False(LinkText.TryGetUri(new string('a', 3000), out _));
    }

    [Fact]
    public void FindAt_GivesUpOnAbsurdlyLongRuns()
    {
        var text = "https://example.com/" + new string('a', 5000);
        Assert.Null(LinkText.FindAt(text, 5));
    }
}
