using Mastemis.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mastemis.Infrastructure.Persistence;

public sealed class OutboxStatus
{
    private long _lastSuccessTicks;
    public DateTimeOffset? LastSuccessUtc => Interlocked.Read(ref _lastSuccessTicks) is var value && value > 0 ? new DateTimeOffset(value, TimeSpan.Zero) : null;
    public void MarkSuccess(DateTimeOffset timestamp) => Interlocked.Exchange(ref _lastSuccessTicks, timestamp.UtcTicks);
}

public sealed class OutboxDispatcher(IServiceScopeFactory scopeFactory, IOutboxPublisher publisher, IClock clock,
    OutboxStatus status, ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            await DispatchBatchAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MastemisDbContext>();
        var now = clock.UtcNow;
        var messages = await db.OutboxMessages.Where(x => x.ProcessedAtUtc == null && x.NextAttemptAtUtc <= now && x.Attempts < 10)
            .OrderBy(x => x.CreatedAtUtc).Take(50).ToListAsync(cancellationToken);
        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message.Id.ToString("D"), message.Type, message.Payload, cancellationToken);
                message.ProcessedAtUtc = clock.UtcNow; message.FailureCode = null; status.MarkSuccess(clock.UtcNow);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.Attempts++;
                message.FailureCode = message.Attempts >= 10 ? "outbox.poison" : "outbox.publish_failed";
                message.NextAttemptAtUtc = clock.UtcNow.AddSeconds(Math.Min(300, Math.Pow(2, message.Attempts)));
                logger.LogWarning("Outbox publication failed for message {MessageId}; attempt {Attempt}.", message.Id, message.Attempts);
            }
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
