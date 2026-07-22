using Mastemis.Application.Administration;
using Mastemis.Application.Queries;

namespace Mastemis.Application.Workers.Queries;

public sealed record WorkerListQuery(string? Search, string? Readiness, int Offset = 0, int Limit = 50);
public sealed record WorkerInventoryItem(Guid WorkerId, string Name, bool Enabled, string CredentialStatus,
    DateTimeOffset? CredentialExpiresAtUtc, DateTimeOffset? LastHeartbeatUtc, bool Ready,
    IReadOnlyList<string> Languages, string? SandboxBackend, int ActiveJobs, int TotalCapacity,
    int UsedCapacity, string? LastFailureCode);

public interface IWorkerInventoryQueryStore
{
    Task<PagedResult<WorkerInventoryItem>> ListAsync(WorkerListQuery query, CancellationToken cancellationToken);
}

public sealed class WorkerInventoryQueryService(IWorkerInventoryQueryStore store, IAdministrationActor actor)
{
    public Task<PagedResult<WorkerInventoryItem>> ListAsync(WorkerListQuery query, CancellationToken cancellationToken)
    {
        if (!actor.IsInRole("Administrator") && !actor.IsInRole("ExamManager"))
            throw new ApplicationFailure(ErrorCodes.Forbidden, "Worker inventory access is not authorized.");
        if (query.Offset < 0 || query.Limit is < 1 or > 100)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "The requested worker page is invalid.");
        return store.ListAsync(query, cancellationToken);
    }
}
