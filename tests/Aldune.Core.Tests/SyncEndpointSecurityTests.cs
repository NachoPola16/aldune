using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class SyncEndpointSecurityTests
{
    [Theory]
    [InlineData("http://sync.example.com/")]
    [InlineData("http://203.0.113.10:8087/")]
    [InlineData("HTTP://Sync.Example.com/")]
    public void PlainHttp_ToAPublicHost_ExposesTheToken(string url)
    {
        Assert.True(SyncEndpointSecurity.ExposesCredentials(url));
    }

    [Theory]
    [InlineData("https://sync.example.com/")]
    [InlineData("http://localhost:8087/")]
    [InlineData("http://127.0.0.1:8087/")]
    [InlineData("http://[::1]:8087/")]
    [InlineData("http://192.168.1.20:8087/")]
    [InlineData("http://10.0.0.5/")]
    [InlineData("http://172.16.4.2/")]
    [InlineData("http://172.31.255.1/")]
    [InlineData("http://nas.local/")]
    [InlineData("http://nas/")]
    [InlineData("http://100.64.1.2/")] // Tailscale / CGNAT
    public void Https_OrAHostInsideTheLocalNetwork_DoesNot(string url)
    {
        Assert.False(SyncEndpointSecurity.ExposesCredentials(url));
    }

    [Theory]
    [InlineData("http://172.32.0.1/")]
    [InlineData("http://192.169.1.1/")]
    public void AddressesJustOutsidePrivateRanges_AreTreatedAsPublic(string url)
    {
        Assert.True(SyncEndpointSecurity.ExposesCredentials(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/")]
    public void AnythingElse_IsNotFlagged(string? url)
    {
        // Las URL no válidas ya las rechaza la validación del transporte; aquí no se avisa dos veces.
        Assert.False(SyncEndpointSecurity.ExposesCredentials(url));
    }

    [Theory]
    [InlineData("http://[2001:db8::1]:8087/")]
    [InlineData("http://[2606:4700::1111]/")]
    public void PlainHttp_ToAPublicIpv6Address_ExposesTheToken(string url)
    {
        // Una IPv6 no lleva puntos: antes se tomaba por un nombre de la red local y no avisaba.
        Assert.True(SyncEndpointSecurity.ExposesCredentials(url));
    }

    [Theory]
    [InlineData("http://[fd00::5]/")]
    [InlineData("http://[fe80::1]/")]
    [InlineData("http://nas.home.arpa/")]
    [InlineData("http://nas.lan/")]
    [InlineData("http://sync.internal/")]
    public void LocalIpv6AndLocalOnlyDomains_DoNot(string url)
    {
        Assert.False(SyncEndpointSecurity.ExposesCredentials(url));
    }
}
