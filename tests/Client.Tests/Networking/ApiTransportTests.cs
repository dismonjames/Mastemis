using System.Net;
using System.Text;
using Mastemis.Client.Core.Networking.Http;
using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Tests.Networking;

public sealed class ApiTransportTests
{
    [Fact]
    public async Task ParsesProblemDetailsWithoutLeakingBody()
    {
        var session = new ClientSession(); session.SelectServer(new("https://server.test"), ClientMode.Connect);
        var client = new HttpClient(new Handler(_ => new(HttpStatusCode.Conflict) { Content = new StringContent("{\"title\":\"Conflict\",\"code\":\"draft.conflict\"}", Encoding.UTF8, "application/problem+json") }));
        var error = await Assert.ThrowsAsync<ApiException>(() => new ApiTransport(client, session).GetAsync<object>("/api/value", TestContext.Current.CancellationToken));
        Assert.Equal("draft.conflict", error.Problem.Code);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response(request)); }
}
