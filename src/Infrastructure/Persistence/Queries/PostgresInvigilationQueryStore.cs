using Mastemis.Application;
using Mastemis.Application.Invigilation.Queries;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Queries;

public sealed class PostgresInvigilationQueryStore(MastemisDbContext db, IClock clock) : IInvigilationQueryStore
{
    public Task<InvigilationSnapshot?> GetExamAsync(Guid examId, int eventLimit, CancellationToken cancellationToken) =>
        BuildAsync(examId, null, eventLimit, cancellationToken);

    public async Task<InvigilationSnapshot?> GetRoomAsync(Guid roomId, int eventLimit, CancellationToken cancellationToken)
    {
        var examId = await db.Rooms.AsNoTracking().Where(x => x.Id == roomId).Select(x => (Guid?)x.ExamId)
            .SingleOrDefaultAsync(cancellationToken);
        return examId is null ? null : await BuildAsync(examId.Value, roomId, eventLimit, cancellationToken);
    }

    private async Task<InvigilationSnapshot?> BuildAsync(Guid examId, Guid? roomId, int eventLimit, CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking().SingleOrDefaultAsync(x => x.Id == examId, ct);
        if (exam is null) return null;
        var sessions = db.ExamSessions.AsNoTracking().Where(x => x.ExamId == examId);
        var rooms = db.Rooms.AsNoTracking().Where(x => x.ExamId == examId);
        if (roomId is { } selectedRoom) { sessions = sessions.Where(x => x.RoomId == selectedRoom); rooms = rooms.Where(x => x.Id == selectedRoom); }
        var roomItems = await rooms.OrderBy(x => x.Name).Select(x => new LiveRoomItem(x.Id, x.Code, x.Name,
            db.ExamSessions.Count(s => s.RoomId == x.Id && s.State == (int)SessionState.Active),
            db.ExamSessions.Count(s => s.RoomId == x.Id && s.State == (int)SessionState.Disconnected),
            db.ConfirmedWarnings.Count(w => w.RoomId == x.Id),
            db.ExamSessions.Count(s => s.RoomId == x.Id && s.State == (int)SessionState.Terminated),
            db.SfeEvents.Where(e => db.ExamSessions.Where(s => s.RoomId == x.Id).Select(s => s.Id).Contains(e.SessionId))
                .Max(e => (DateTimeOffset?)e.ServerReceivedAtUtc))).ToArrayAsync(ct);
        var candidates = await (from session in sessions
                                join candidate in db.Candidates.AsNoTracking() on session.CandidateId equals candidate.Id
                                join user in db.Users.AsNoTracking() on candidate.UserId equals user.Id
                                orderby user.DisplayName
                                select new LiveCandidateItem(candidate.Id, session.Id, session.RoomId, user.DisplayName,
                                    ((SessionState)session.State).ToString(), ConnectionState(session.State),
                                    db.SfeEvents.Count(e => e.SessionId == session.Id),
                                    db.SfeEvaluations.Count(e => e.SessionId == session.Id),
                                    db.ConfirmedWarnings.Count(w => w.SessionId == session.Id),
                                    session.State == (int)SessionState.Terminated,
                                    db.SfeEvents.Count(e => e.SessionId == session.Id && !db.SfeEvaluations.Select(v => v.EventId).Contains(e.Id)),
                                    db.SfeEvents.Where(e => e.SessionId == session.Id).Max(e => (DateTimeOffset?)e.ServerReceivedAtUtc) ?? session.StartedAtUtc))
            .Take(1000).ToArrayAsync(ct);
        var sessionIds = sessions.Select(x => x.Id);
        var warnings = await db.ConfirmedWarnings.AsNoTracking().Where(x => sessionIds.Contains(x.SessionId))
            .OrderByDescending(x => x.IssuedAtUtc).Take(eventLimit)
            .Select(x => new LiveWarningItem(x.Id, x.SessionId, x.CandidateId, x.Ordinal,
                x.Ordinal >= 3 ? "Critical" : x.Ordinal == 2 ? "High" : "Warning", x.IssuedAtUtc)).ToArrayAsync(ct);
        var events = await db.SfeEvents.AsNoTracking().Where(x => sessionIds.Contains(x.SessionId))
            .OrderByDescending(x => x.ServerReceivedAtUtc).Take(eventLimit)
            .Select(x => new LiveEventItem(x.Id, x.SessionId, x.EventType,
                db.SfeEvaluations.Where(e => e.EventId == x.Id).Select(e => e.Result.ToString()).FirstOrDefault() ?? "Unreviewed",
                x.ServerReceivedAtUtc)).ToArrayAsync(ct);
        return new(exam.Id, exam.Title, ((ExamState)exam.State).ToString(), roomItems, candidates, warnings, events, clock.UtcNow);
    }

    private static string ConnectionState(int state) => state == (int)SessionState.Active ? "Connected"
        : state == (int)SessionState.Disconnected ? "Disconnected" : "Inactive";
}
