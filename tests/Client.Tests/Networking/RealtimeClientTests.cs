using Mastemis.Client.Core.Networking.Realtime;
using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Tests.Networking;

public sealed class RealtimeClientTests
{
    [Fact]
    public async Task DuplicateMessageIdIsDeliveredOnce()
    {
        var client = new RealtimeClient(new ClientSession(), new ImmediateUiDispatcher());
        var count = 0; client.EventReceived += (_, _) => count++;
        var value = new RealtimeEnvelope("message-1", 1, "GenerationProgressed", DateTimeOffset.UtcNow, "{}");
        await client.ProcessEnvelopeAsync(value, TestContext.Current.CancellationToken);
        await client.ProcessEnvelopeAsync(value, TestContext.Current.CancellationToken);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task UnknownContractVersionIsIgnored()
    {
        var client = new RealtimeClient(new ClientSession(), new ImmediateUiDispatcher());
        var count = 0; client.EventReceived += (_, _) => count++;
        await client.ProcessEnvelopeAsync(new("message-2", 99, "Future", DateTimeOffset.UtcNow, "{}"), TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }
}
