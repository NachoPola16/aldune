using Aldune.Core;

namespace Aldune.Core.Tests;

public sealed class CleartextUrlTests
{
    [Theory]
    [InlineData("http://sync.example.com/")]
    [InlineData("http://8.8.8.8:8087")]
    [InlineData("HTTP://Sync.Example.com")]
    [InlineData("http://172.32.0.1")]
    public void PlainHttpToAPublicHostNeedsConfirmation(string url) => Assert.True(CleartextUrl.IsOutsideLan(url));

    [Theory]
    [InlineData("https://sync.example.com/")]
    [InlineData("http://192.168.1.20:8087")]
    [InlineData("http://10.0.0.5")]
    [InlineData("http://172.16.0.1")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://localhost:8080")]
    [InlineData("http://nas")]
    [InlineData("http://nas.local")]
    [InlineData("http://[::1]:8080")]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData(null)]
    public void HttpsLanAndNonUrlsDoNot(string? url) => Assert.False(CleartextUrl.IsOutsideLan(url));
}
