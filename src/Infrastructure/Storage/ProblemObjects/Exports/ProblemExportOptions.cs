namespace Mastemis.Infrastructure.Storage.ProblemObjects.Exports;

public sealed record ProblemExportOptions(TimeSpan Retention, int CleanupBatchSize)
{
    public int BoundedBatchSize => Math.Clamp(CleanupBatchSize, 1, 1000);
}
