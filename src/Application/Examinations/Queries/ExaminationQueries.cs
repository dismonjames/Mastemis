using Mastemis.Application.Administration;
using Mastemis.Application.Queries;

namespace Mastemis.Application.Examinations.Queries;

public sealed record ExaminationListQuery(string? Search, string? Status, DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc, int Offset = 0, int Limit = 50);

public sealed record ExaminationListItem(Guid Id, string Title, string Status, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartsAtUtc, DateTimeOffset? EndsAtUtc, int RoomCount, int CandidateCount,
    int ActiveSessionCount, int WarningCount);

public sealed record ExaminationRoomSummary(Guid Id, string Code, string Name, int CandidateCount,
    int ActiveSessionCount, int DisconnectedSessionCount, int WarningCount);

public sealed record ExaminationCandidateSummary(Guid CandidateId, Guid UserId, string DisplayName,
    string AccessState, string? SessionState, Guid? RoomId, int WarningCount);

public sealed record ExaminationSessionSummary(Guid SessionId, Guid CandidateId, Guid RoomId, string State,
    DateTimeOffset? StartedAtUtc, DateTimeOffset? TerminatedAtUtc, int WarningCount);

public sealed record ExaminationProblemSummary(Guid ProblemId, string Title, int Version, DateTimeOffset AssignedAtUtc);
public sealed record ExaminationTimelineItem(string Type, DateTimeOffset OccurredAtUtc, string Summary);

public sealed record ExaminationDetails(ExaminationListItem Examination,
    IReadOnlyList<ExaminationRoomSummary> Rooms,
    IReadOnlyList<ExaminationCandidateSummary> Candidates,
    IReadOnlyList<ExaminationSessionSummary> Sessions,
    IReadOnlyList<ExaminationProblemSummary> Problems,
    IReadOnlyList<ExaminationTimelineItem> Timeline);

public interface IExaminationQueryStore
{
    Task<PagedResult<ExaminationListItem>> ListAsync(IAdministrationActor actor, ExaminationListQuery query,
        CancellationToken cancellationToken);
    Task<ExaminationDetails?> GetAsync(Guid examId, CancellationToken cancellationToken);
}

public sealed class ExaminationQueryService(IExaminationQueryStore store, IAdministrationActor actor,
    IAuthorizationService authorization)
{
    public Task<PagedResult<ExaminationListItem>> ListAsync(ExaminationListQuery query, CancellationToken cancellationToken)
    {
        ValidatePage(query.Offset, query.Limit);
        return store.ListAsync(actor, query, cancellationToken);
    }

    public async Task<ExaminationDetails> GetAsync(Guid examId, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("exam.realtime", examId, cancellationToken);
        return await store.GetAsync(examId, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Examination not found.");
    }

    private static void ValidatePage(int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > 100)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "The requested examination page is invalid.");
    }
}
