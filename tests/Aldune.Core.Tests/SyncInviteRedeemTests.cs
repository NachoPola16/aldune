using System.Net;
using System.Text;
using Aldune.Core;

namespace Aldune.Core.Tests;

public sealed class SyncInviteRedeemTests
{
    private sealed class Reply(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    [Fact]
    public async Task ARedeemReplyWithATokenReturnsIt()
    {
        using var http = new HttpClient(new Reply(HttpStatusCode.OK, """{"token":"tok123","space":"u-abcd1234"}"""));

        Assert.Equal("tok123", await SyncInviteClient.RedeemAsync(http, "https://sync.example.com/", "code"));
    }

    [Theory]
    [InlineData("""{"token":null}""")]
    [InlineData("""{"token":""}""")]
    [InlineData("""{"space":"u-abcd1234"}""")]
    [InlineData("[]")]
    public async Task ARedeemReplyWithoutAUsableTokenIsAServerFailureNotACrash(string body)
    {
        using var http = new HttpClient(new Reply(HttpStatusCode.OK, body));

        var error = await Assert.ThrowsAsync<InviteException>(() => SyncInviteClient.RedeemAsync(http, "https://sync.example.com/", "code"));

        Assert.Equal(InviteFailure.Server, error.Failure);
    }
}
