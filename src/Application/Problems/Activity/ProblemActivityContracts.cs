namespace Mastemis.Application.Problems.Activity;

public sealed record ProblemActivityQuery(int Offset = 0, int Limit = 50, string? Kind = null);
public sealed record ProblemActivityEntry(Guid EventId, Guid ProblemId, DateTimeOffset Timestamp,
    string Actor, string Kind, string Summary, string? CorrelationId, Guid? RelatedOperationId);
public sealed record ProblemActivityPage(IReadOnlyList<ProblemActivityEntry> Items, int Offset, int Limit, bool HasMore);

public interface IProblemActivityStore
{
    Task<ProblemActivityPage> ListAsync(Guid problemId, ProblemActivityQuery query, CancellationToken cancellationToken);
}

public sealed class ProblemActivityService(IProblemActivityStore store, IAuthorizationService authorization)
{
    public async Task<ProblemActivityPage> ListAsync(Guid problemId, ProblemActivityQuery query, CancellationToken ct)
    {
        if (query.Offset < 0 || query.Limit is < 1 or > 100) throw new ApplicationFailure(ErrorCodes.InvalidInput, "Activity page is invalid.");
        await authorization.EnsureAsync("problem.read", problemId, ct);
        return await store.ListAsync(problemId, query, ct);
    }
}
