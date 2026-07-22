using Mastemis.Application.Queries;

namespace Mastemis.Application.Candidates.Queries;

public sealed record CandidateListQuery(string? Search, string? AccessState, string? SessionState,
    Guid? RoomId, int Offset = 0, int Limit = 50);

public sealed record CandidateListItem(Guid CandidateId, Guid UserId, string Username, string DisplayName,
    bool AccountEnabled, string RegistrationCode, string AccessState, Guid? SessionId, string? SessionState,
    Guid? RoomId, int WarningCount, DateTimeOffset? LatestActivityUtc);

public interface ICandidateQueryStore
{
    Task<PagedResult<CandidateListItem>> ListAsync(Guid examId, CandidateListQuery query,
        CancellationToken cancellationToken);
    Task<CandidateListItem?> GetAsync(Guid examId, Guid candidateId, CancellationToken cancellationToken);
}

public sealed class CandidateQueryService(ICandidateQueryStore store, IAuthorizationService authorization)
{
    public async Task<PagedResult<CandidateListItem>> ListAsync(Guid examId, CandidateListQuery query,
        CancellationToken cancellationToken)
    {
        Validate(query.Offset, query.Limit);
        await authorization.EnsureAsync("exam.realtime", examId, cancellationToken);
        return await store.ListAsync(examId, query, cancellationToken);
    }

    public async Task<CandidateListItem> GetAsync(Guid examId, Guid candidateId, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("exam.realtime", examId, cancellationToken);
        return await store.GetAsync(examId, candidateId, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Candidate not found.");
    }

    private static void Validate(int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > 100)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "The requested candidate page is invalid.");
    }
}
