using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Dashboard;

public sealed record DashboardRoom(Guid RoomId, Guid ExamId, string Name, int ActiveCandidates, int DisconnectedCandidates, int WarningCount, int TerminatedCandidates);
public sealed record DashboardWarning(Guid WarningId, Guid ExamId, Guid RoomId, Guid CandidateId, int Ordinal, DateTimeOffset IssuedAtUtc);
public sealed record DashboardGeneration(Guid OperationId, Guid ProblemId, string Status, int ProgressNumerator, int ProgressDenominator, DateTimeOffset UpdatedAtUtc);
public sealed record DashboardSnapshot(string Audience, int ActiveExaminations, int ScheduledExaminations, int ActiveCandidates,
    int DisconnectedCandidates, int PendingJudgeJobs, int ActiveWorkers, int TotalWorkerCapacity, int UsedWorkerCapacity,
    int WarningCount, int TerminatedSessions, Guid? AssignedExamId, Guid? AssignedRoomId, Guid? ActiveSessionId,
    DateTimeOffset? ExamStartsAtUtc, DateTimeOffset? ExamEndsAtUtc, string? SessionState, int SubmittedProblems,
    int PendingJudgements, IReadOnlyList<DashboardRoom> Rooms, IReadOnlyList<DashboardWarning> RecentWarnings,
    IReadOnlyList<DashboardGeneration> RecentGenerations, IReadOnlyList<string> DegradedComponents);
public interface IDashboardClient { Task<DashboardSnapshot?> GetAsync(CancellationToken cancellationToken); }
public sealed class DashboardClient(IApiTransport transport) : IDashboardClient
{
    public Task<DashboardSnapshot?> GetAsync(CancellationToken cancellationToken) => transport.GetAsync<DashboardSnapshot>("/api/queries/dashboard", cancellationToken);
}
