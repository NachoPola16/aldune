using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class WebDavHrefTests
{
    private static readonly Uri Objects = new("https://nube.example.com/remote.php/dav/files/ana/aldune/objects/");

    [Fact]
    public void ARelativeHref_ResolvesAgainstTheServer()
    {
        var uri = WebDavSyncTransport.ResolveHref(Objects, "/remote.php/dav/files/ana/aldune/objects/a.json");
        Assert.Equal("https://nube.example.com/remote.php/dav/files/ana/aldune/objects/a.json", uri?.ToString());
    }

    [Fact]
    public void AnAbsoluteHrefToTheSameServer_IsKept()
    {
        var uri = WebDavSyncTransport.ResolveHref(Objects, "https://nube.example.com/remote.php/dav/files/ana/aldune/objects/a.json");
        Assert.Equal("nube.example.com", uri?.Host);
    }

    [Fact]
    public void AnAbsoluteHrefToAnotherHost_KeepsOnlyItsPathOnOurServer()
    {
        // Lo que importa: la petición nunca sale hacia el host del href, así que las credenciales no viajan allí.
        var uri = WebDavSyncTransport.ResolveHref(Objects, "http://atacante.example.net:8080/remote.php/dav/files/ana/aldune/objects/a.json");
        Assert.Equal("nube.example.com", uri?.Host);
        Assert.Equal("https", uri?.Scheme);
        Assert.Equal(443, uri?.Port);
    }

    [Theory]
    [InlineData("//atacante.example.net/x.json")]
    [InlineData("\\\\atacante.example.net\\x.json")]
    public void ASchemeRelativeOrUncHref_IsNeverFollowedToAnotherHost(string href)
    {
        var uri = WebDavSyncTransport.ResolveHref(Objects, href);
        Assert.True(uri is null || uri.Host == "nube.example.com");
    }
}
