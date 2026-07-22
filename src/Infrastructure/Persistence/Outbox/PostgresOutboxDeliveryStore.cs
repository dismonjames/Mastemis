using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Outbox;

public sealed class PostgresOutboxDeliveryStore(MastemisDbContext db) : IOutboxDeliveryStore
{
    public async Task ProcessClaimedBatchAsync(DateTimeOffset now, int limit,
        Func<OutboxRow, CancellationToken, Task> process, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var messages = await db.OutboxMessages.FromSqlInterpolated($$"""
            SELECT * FROM outbox_messages
            WHERE "ProcessedAtUtc" IS NULL AND "NextAttemptAtUtc" <= {{now}} AND "Attempts" < 10
            ORDER BY "CreatedAtUtc"
            FOR UPDATE SKIP LOCKED
            LIMIT {{Math.Clamp(limit, 1, 100)}}
            """).ToListAsync(cancellationToken);
        foreach (var message in messages) await process(message, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
