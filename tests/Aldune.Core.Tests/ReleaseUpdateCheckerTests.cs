using System.Net;
using System.Text;
using Aldune.Core;

namespace Aldune.Core.Tests;

public class ReleaseUpdateCheckerTests
{
    [Theory]
    [InlineData("v0.9.0", "0.8.1", true)]
    [InlineData("v0.10.0", "0.9.0", true)]
    [InlineData("v1.0.0", "0.99.99", true)]
    [InlineData("v0.8.1", "0.8.1.0", false)]
    [InlineData("v1.2", "1.2.0", false)]
    [InlineData("v0.8.0", "0.8.1", false)]
    public async Task CheckAsync_ComparesNumericNormalizedVersions(string tag, string current, bool newer)
    {
        using var client = Client(Release(tag));
        var result = await new ReleaseUpdateChecker(client).CheckAsync(current);
        Assert.Equal(newer, result is not null);
    }

    [Fact]
    public async Task CheckAsync_UsesGitHubHeadersAndOnlyKnownReleaseUrl()
    {
        using var client = new HttpClient(new FakeHandler((request, _) =>
        {
            Assert.Equal(ReleaseUpdateChecker.LatestReleaseApi, request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Contains("Aldune/", request.Headers.UserAgent.ToString());
            Assert.Contains("application/vnd.github+json", request.Headers.Accept.ToString());
            return Task.FromResult(Response(Release("v1.2.3", extra: ",\"html_url\":\"https://evil.example/install.exe\"")));
        }));
        var update = await new ReleaseUpdateChecker(client).CheckAsync("0.8.1");
        Assert.NotNull(update);
        Assert.Equal("https://github.com/NachoPola16/aldune/releases/tag/v1.2.3", update.DownloadUri.AbsoluteUri);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CheckAsync_RejectsDraftAndPrerelease(bool draft, bool prerelease)
    {
        using var client = Client(Release("v9.0.0", draft, prerelease));
        Assert.Null(await new ReleaseUpdateChecker(client).CheckAsync("0.8.1"));
    }

    [Theory]
    [InlineData("v1.2.3-beta")]
    [InlineData("v1.2.3+build")]
    [InlineData("../../evil")]
    [InlineData("https://evil.example")]
    [InlineData("v1.2.3/../../evil")]
    [InlineData("1.2.3 ")]
    [InlineData("1.2.-3")]
    [InlineData("1.2.999999999999")]
    public async Task CheckAsync_RejectsUnsafeOrNonStableTags(string tag)
    {
        using var client = Client(Release(tag));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseUpdateChecker(client).CheckAsync("0.8.1"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"draft\":false,\"prerelease\":false}")]
    public async Task CheckAsync_RejectsMissingMetadata(string json)
    {
        using var client = Client(json);
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseUpdateChecker(client).CheckAsync("0.8.1"));
    }

    [Fact]
    public async Task CheckAsync_PropagatesMalformedJson()
    {
        using var client = Client("not json");
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => new ReleaseUpdateChecker(client).CheckAsync("0.8.1"));
    }

    [Theory]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task CheckAsync_HttpErrorsAreNotReportedAsUpToDate(int status)
    {
        using var client = Client("{}", (HttpStatusCode)status);
        await Assert.ThrowsAsync<HttpRequestException>(() => new ReleaseUpdateChecker(client).CheckAsync("0.8.1"));
    }

    [Fact]
    public async Task CheckAsync_PropagatesNetworkErrors()
    {
        using var client = new HttpClient(new FakeHandler((_, _) => throw new HttpRequestException("offline")));
        await Assert.ThrowsAsync<HttpRequestException>(() => new ReleaseUpdateChecker(client).CheckAsync("0.8.1"));
    }

    [Fact]
    public async Task CheckAsync_HonorsCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new HttpClient(new FakeHandler(async (_, token) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return Response("{}");
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ReleaseUpdateChecker(client).CheckAsync("0.8.1", cancellation.Token));
    }

    [Fact]
    public async Task CheckAsync_HonorsInjectedClientTimeout()
    {
        using var client = new HttpClient(new FakeHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Response("{}");
        })) { Timeout = TimeSpan.FromMilliseconds(50) };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ReleaseUpdateChecker(client).CheckAsync("0.8.1"));
    }

    [Fact]
    public async Task CheckAsync_InvalidCurrentVersionDoesNotSendRequest()
    {
        using var client = new HttpClient(new FakeHandler((_, _) => throw new InvalidOperationException("Should not send")));
        await Assert.ThrowsAsync<ArgumentException>(() => new ReleaseUpdateChecker(client).CheckAsync("not a version"));
    }

    [Theory]
    [InlineData("v1.2", "1.2.0.0")]
    [InlineData("V1.2.3", "1.2.3.0")]
    [InlineData("1.2.3.4", "1.2.3.4")]
    [InlineData("01.02.03", "1.2.3.0")]
    public void TryParseVersion_NormalizesNumericParts(string text, string expected)
    {
        Assert.True(ReleaseUpdateChecker.TryParseVersion(text, out var version));
        Assert.Equal(new Version(expected), version);
    }

    private static string Release(string tag, bool draft = false, bool prerelease = false, string extra = "") =>
        $"{{\"tag_name\":\"{tag}\",\"draft\":{draft.ToString().ToLowerInvariant()},\"prerelease\":{prerelease.ToString().ToLowerInvariant()}{extra}}}";

    private static HttpResponseMessage Response(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpClient Client(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new FakeHandler((_, _) => Task.FromResult(Response(json, status))));

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
