using Mastemis.Application;
using Microsoft.Extensions.Logging;

namespace Mastemis.Infrastructure.Persistence.Outbox;

public sealed class OutboxBatchProcessor(IOutboxDeliveryStore store, IOutboxPublisher publisher, IClock clock,
    OutboxStatus status, ILogger<OutboxBatchProcessor> logger)
{
    public Task ProcessAsync(CancellationToken cancellationToken) => store.ProcessClaimedBatchAsync(
        clock.UtcNow, 50, ProcessMessageAsync, cancellationToken);

    private async Task ProcessMessageAsync(OutboxRow message, CancellationToken cancellationToken)
    {
        try
        {
            await publisher.PublishAsync(message.Id.ToString("D"), message.Type, message.Payload, cancellationToken);
            message.ProcessedAtUtc = clock.UtcNow;
            message.FailureCode = null;
            status.MarkSuccess(clock.UtcNow);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            message.Attempts++;
            message.FailureCode = message.Attempts >= 10 ? "outbox.poison" : "outbox.publish_failed";
            message.NextAttemptAtUtc = clock.UtcNow.AddSeconds(Math.Min(300, Math.Pow(2, message.Attempts)));
            logger.LogWarning("Outbox publication failed for message {MessageId}; attempt {Attempt}.", message.Id, message.Attempts);
        }
    }
}
