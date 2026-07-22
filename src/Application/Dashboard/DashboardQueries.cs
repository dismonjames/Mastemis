using Mastemis.Application.Administration;

namespace Mastemis.Application.Dashboard;

public sealed record DashboardWarningItem(Guid WarningId, Guid ExamId, Guid RoomId, Guid CandidateId,
    int Ordinal, DateTimeOffset IssuedAtUtc);

public sealed record DashboardGenerationItem(Guid OperationId, Guid ProblemId, string Status,
    int ProgressNumerator, int ProgressDenominator, DateTimeOffset UpdatedAtUtc);

public sealed record DashboardRoomItem(Guid RoomId, Guid ExamId, string Name, int ActiveCandidates,
    int DisconnectedCandidates, int WarningCount, int TerminatedCandidates);

public sealed record DashboardSnapshot(
    string Audience,
    int ActiveExaminations,
    int ScheduledExaminations,
    int ActiveCandidates,
    int DisconnectedCandidates,
    int PendingJudgeJobs,
    int ActiveWorkers,
    int TotalWorkerCapacity,
    int UsedWorkerCapacity,
    int WarningCount,
    int TerminatedSessions,
    Guid? AssignedExamId,
    Guid? AssignedRoomId,
    Guid? ActiveSessionId,
    DateTimeOffset? ExamStartsAtUtc,
    DateTimeOffset? ExamEndsAtUtc,
    string? SessionState,
    int SubmittedProblems,
    int PendingJudgements,
    IReadOnlyList<DashboardRoomItem> Rooms,
    IReadOnlyList<DashboardWarningItem> RecentWarnings,
    IReadOnlyList<DashboardGenerationItem> RecentGenerations,
    IReadOnlyList<string> DegradedComponents);

public interface IDashboardQueryStore
{
    Task<DashboardSnapshot> GetAsync(IAdministrationActor actor, CancellationToken cancellationToken);
}

public sealed class DashboardQueryService(IDashboardQueryStore store, IAdministrationActor actor)
{
    public Task<DashboardSnapshot> GetAsync(CancellationToken cancellationToken) =>
        store.GetAsync(actor, cancellationToken);
}
