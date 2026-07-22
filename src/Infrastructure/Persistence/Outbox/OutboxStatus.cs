namespace Mastemis.Infrastructure.Persistence.Outbox;

public sealed class OutboxStatus
{
    private long _lastSuccessTicks;
    public DateTimeOffset? LastSuccessUtc => Interlocked.Read(ref _lastSuccessTicks) is var value && value > 0
        ? new DateTimeOffset(value, TimeSpan.Zero) : null;
    public void MarkSuccess(DateTimeOffset timestamp) => Interlocked.Exchange(ref _lastSuccessTicks, timestamp.UtcTicks);
}
