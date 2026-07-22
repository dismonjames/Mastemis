using Mastemis.Application;
using Mastemis.Server.Realtime;
using Microsoft.AspNetCore.SignalR;

public sealed class SignalROutboxPublisher(IHubContext<ExamHub> hub, IServiceScopeFactory scopeFactory, IClock clock)
    : IOutboxPublisher
{
    public async Task PublishAsync(string messageId, string messageType, string payload, CancellationToken cancellationToken)
    {
        if (!RealtimeContractCatalog.IsSupported(messageType)) throw new InvalidDataException("Unsupported realtime contract type.");
        var envelope = new RealtimeEnvelope(messageId, 1, messageType, clock.UtcNow, payload);
        await using var scope = scopeFactory.CreateAsyncScope();
        var targets = await scope.ServiceProvider.GetRequiredService<RealtimeRouteResolver>().ResolveAsync(payload, cancellationToken);
        foreach (var target in targets) await hub.Clients.Group(target).SendAsync("mastemis.event", envelope, cancellationToken);
    }
}
