using Mastemis.Contracts.Judge;

namespace Mastemis.Judge.Workspaces;

public interface IJudgeWorkspaceManager
{
    ValueTask<JudgeWorkspace> CreateAsync(CancellationToken cancellationToken);
    ValueTask<int> ReconcileStaleAsync(TimeSpan minimumAge, int batchSize, CancellationToken cancellationToken);
}

public sealed record MaterializedSource(string OriginalName, string InternalPath);
