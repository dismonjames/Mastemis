using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Invigilation;

public sealed record LiveCandidate(Guid CandidateId, Guid SessionId, Guid RoomId, string DisplayName,
    string SessionState, string ConnectionState, int RawEventCount, int EvaluatedEventCount, int WarningCount,
    bool Terminated, int UnresolvedEventCount, DateTimeOffset? LatestActivityUtc);
public sealed record LiveRoom(Guid RoomId, string Code, string Name, int ConnectedCandidates, int DisconnectedCandidates,
    int WarningCount, int TerminatedCandidates, DateTimeOffset? LatestActivityUtc);
public sealed record InvigilationSnapshot(Guid ExamId, string Title, string ExamState, IReadOnlyList<LiveRoom> Rooms,
    IReadOnlyList<LiveCandidate> Candidates, IReadOnlyList<object> RecentWarnings, IReadOnlyList<object> RecentEvents,
    DateTimeOffset GeneratedAtUtc);
public interface IInvigilationClient { Task<InvigilationSnapshot?> GetExamAsync(Guid examId, CancellationToken cancellationToken); }
public sealed class InvigilationClient(IApiTransport transport) : IInvigilationClient
{
    public Task<InvigilationSnapshot?> GetExamAsync(Guid examId, CancellationToken cancellationToken) =>
        transport.GetAsync<InvigilationSnapshot>($"/api/queries/invigilation/exams/{examId:D}?eventLimit=100", cancellationToken);
}
