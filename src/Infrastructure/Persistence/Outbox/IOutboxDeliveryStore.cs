namespace Mastemis.Infrastructure.Persistence.Outbox;

public interface IOutboxDeliveryStore
{
    Task ProcessClaimedBatchAsync(DateTimeOffset now, int limit,
        Func<OutboxRow, CancellationToken, Task> process, CancellationToken cancellationToken);
}
