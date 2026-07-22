namespace Mastemis.Application.Invigilation.Queries;

public sealed record LiveWarningItem(Guid WarningId, Guid SessionId, Guid CandidateId, int Ordinal,
    string Severity, DateTimeOffset IssuedAtUtc);
public sealed record LiveEventItem(Guid EventId, Guid SessionId, string EventType, string EvaluationState,
    DateTimeOffset ReceivedAtUtc);
public sealed record LiveCandidateItem(Guid CandidateId, Guid SessionId, Guid RoomId, string DisplayName,
    string SessionState, string ConnectionState, int RawEventCount, int EvaluatedEventCount, int WarningCount,
    bool Terminated, int UnresolvedEventCount, DateTimeOffset? LatestActivityUtc);
public sealed record LiveRoomItem(Guid RoomId, string Code, string Name, int ConnectedCandidates,
    int DisconnectedCandidates, int WarningCount, int TerminatedCandidates, DateTimeOffset? LatestActivityUtc);
public sealed record InvigilationSnapshot(Guid ExamId, string Title, string ExamState,
    IReadOnlyList<LiveRoomItem> Rooms, IReadOnlyList<LiveCandidateItem> Candidates,
    IReadOnlyList<LiveWarningItem> RecentWarnings, IReadOnlyList<LiveEventItem> RecentEvents,
    DateTimeOffset GeneratedAtUtc);

public interface IInvigilationQueryStore
{
    Task<InvigilationSnapshot?> GetExamAsync(Guid examId, int eventLimit, CancellationToken cancellationToken);
    Task<InvigilationSnapshot?> GetRoomAsync(Guid roomId, int eventLimit, CancellationToken cancellationToken);
}

public sealed class InvigilationQueryService(IInvigilationQueryStore store, IAuthorizationService authorization)
{
    public async Task<InvigilationSnapshot> GetExamAsync(Guid examId, int eventLimit, CancellationToken cancellationToken)
    {
        Validate(eventLimit);
        await authorization.EnsureAsync("exam.realtime", examId, cancellationToken);
        return await store.GetExamAsync(examId, eventLimit, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Examination not found.");
    }

    public async Task<InvigilationSnapshot> GetRoomAsync(Guid roomId, int eventLimit, CancellationToken cancellationToken)
    {
        Validate(eventLimit);
        await authorization.EnsureAsync("room.realtime", roomId, cancellationToken);
        return await store.GetRoomAsync(roomId, eventLimit, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Room not found.");
    }

    private static void Validate(int limit)
    {
        if (limit is < 1 or > 200) throw new ApplicationFailure(ErrorCodes.InvalidInput, "Event limit must be between 1 and 200.");
    }
}
