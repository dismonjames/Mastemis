namespace Mastemis.Infrastructure.Storage.Reconciliation;

public sealed record SourceReconciliationOptions(string RootPath, TimeSpan OrphanAge, TimeSpan ScanInterval, int BatchSize)
{
    public int BoundedBatchSize => Math.Clamp(BatchSize, 1, 1000);
}

public sealed class SourceReconciliationStatus
{
    private long _lastSuccessTicks;
    private int _failed;
    public DateTimeOffset? LastSuccessUtc => Interlocked.Read(ref _lastSuccessTicks) is var ticks && ticks > 0 ? new(ticks, TimeSpan.Zero) : null;
    public bool Failed => Volatile.Read(ref _failed) != 0;
    public void MarkSuccess(DateTimeOffset timestamp) { Interlocked.Exchange(ref _lastSuccessTicks, timestamp.UtcTicks); Volatile.Write(ref _failed, 0); }
    public void MarkFailure() => Volatile.Write(ref _failed, 1);
}
