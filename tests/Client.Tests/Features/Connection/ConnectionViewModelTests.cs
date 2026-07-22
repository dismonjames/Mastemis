using Mastemis.Client.Core.Features.Connection;
using Mastemis.Client.Core.Networking.Http;
using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Tests.Features.Connection;

public sealed class ConnectionViewModelTests
{
    [Theory]
    [InlineData("https://example.test", true)]
    [InlineData("http://localhost:5000", true)]
    [InlineData("http://example.test", false)]
    [InlineData("file:///tmp/server", false)]
    [InlineData("https://user:secret@example.test", false)]
    public void UrlPolicyRejectsUnsafeProductionConnections(string value, bool expected)
        => Assert.Equal(expected, ConnectionViewModel.TryNormalizeUri(value, out _));

    [Fact]
    public async Task SuccessfulProbeSelectsServer()
    {
        var session = new ClientSession();
        var model = new ConnectionViewModel(new Probe(new(true, true, "1.0", null)), session) { ServerUrl = "https://example.test" };
        model.TestConnectionCommand.Execute(null);
        await WaitUntilAsync(() => model.StatusTitle is not null);
        Assert.Equal("Connected", model.StatusTitle);
        Assert.Equal(new Uri("https://example.test/"), session.ServerBaseUri);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(5);
    }

    private sealed class Probe(ServerProbeResult result) : IServerProbe
    {
        public Task<ServerProbeResult> ProbeAsync(Uri baseUri, CancellationToken cancellationToken) => Task.FromResult(result);
    }
}
