namespace Mastemis.Mas.Runtime.Limits;

public sealed record MasRuntimeLimits(int MaximumTests = 10_000, int MaximumCollectionLength = 1_000_000,
    int MaximumGraphNodes = 100_000, int MaximumGraphEdges = 1_000_000, long MaximumSteps = 10_000_000,
    long MaximumOutputBytes = 64 * 1024 * 1024, TimeSpan? MaximumDuration = null)
{
    public TimeSpan Duration => MaximumDuration ?? TimeSpan.FromSeconds(10);
}
