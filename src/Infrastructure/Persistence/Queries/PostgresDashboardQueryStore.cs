using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Application.Dashboard;
using Mastemis.Application.Problems.Authoring;
using Mastemis.Domain;
using Mastemis.Infrastructure.Persistence.Problems;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Queries;

public sealed class PostgresDashboardQueryStore(MastemisDbContext db, IClock clock) : IDashboardQueryStore
{
    public async Task<DashboardSnapshot> GetAsync(IAdministrationActor actor, CancellationToken cancellationToken)
    {
        var examIds = AccessibleExamIds(actor);
        var roomIds = AccessibleRoomIds(actor, examIds);
        var sessions = db.ExamSessions.AsNoTracking().Where(x => examIds.Contains(x.ExamId));
        var warnings = db.ConfirmedWarnings.AsNoTracking().Where(x => examIds.Contains(x.ExamId));
        var exams = db.Exams.AsNoTracking().Where(x => examIds.Contains(x.Id));
        var candidateId = actor.IsInRole(MastemisRoles.Candidate)
            ? await db.Candidates.Where(x => x.UserId == actor.UserId.Value).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken)
            : null;
        if (candidateId is { } own) sessions = sessions.Where(x => x.CandidateId == own);

        var activeSession = await sessions.Where(x => x.State == (int)SessionState.Active || x.State == (int)SessionState.Disconnected)
            .OrderByDescending(x => x.StartedAtUtc).FirstOrDefaultAsync(cancellationToken);
        var assignedExam = activeSession?.ExamId ?? await examIds.Cast<Guid?>().FirstOrDefaultAsync(cancellationToken);
        var assignedRoom = actor.IsInRole(MastemisRoles.RoomInvigilator)
            ? await roomIds.Cast<Guid?>().FirstOrDefaultAsync(cancellationToken) : activeSession?.RoomId;
        var schedule = assignedExam is { } examId
            ? await db.Exams.AsNoTracking().Where(x => x.Id == examId).Select(x => new { x.StartsAtUtc, x.EndsAtUtc }).FirstOrDefaultAsync(cancellationToken)
            : null;
        var workerCutoff = clock.UtcNow.AddMinutes(-2);
        var activeWorkers = db.JudgeWorkers.AsNoTracking().Where(x => x.IsEnabled && x.LastHeartbeatUtc >= workerCutoff);
        var normalUsed = await db.JudgeJobs.CountAsync(x => x.State == (int)JudgeJobState.Claimed || x.State == (int)JudgeJobState.Running, cancellationToken);
        var referenceUsed = await db.ReferenceOutputJobs.CountAsync(x => x.Status == 1 || x.Status == 2, cancellationToken);
        var warningItems = await warnings.OrderByDescending(x => x.IssuedAtUtc).Take(8)
            .Select(x => new DashboardWarningItem(x.Id, x.ExamId, x.RoomId, x.CandidateId, x.Ordinal, x.IssuedAtUtc))
            .ToArrayAsync(cancellationToken);
        var problemIds = AccessibleProblemIds(actor);
        var generations = await db.ProblemGenerationOperations.AsNoTracking().Where(x => problemIds.Contains(x.ProblemId))
            .OrderByDescending(x => x.UpdatedAtUtc).Take(8)
            .Select(x => new DashboardGenerationItem(x.Id, x.ProblemId, StatusName(x.Status), x.ProgressNumerator, x.ProgressDenominator, x.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);
        var roomItems = await (from room in db.Rooms.AsNoTracking().Where(x => roomIds.Contains(x.Id))
                               select new DashboardRoomItem(room.Id, room.ExamId, room.Name,
                                   db.ExamSessions.Count(x => x.RoomId == room.Id && x.State == (int)SessionState.Active),
                                   db.ExamSessions.Count(x => x.RoomId == room.Id && x.State == (int)SessionState.Disconnected),
                                   db.ConfirmedWarnings.Count(x => x.RoomId == room.Id),
                                   db.ExamSessions.Count(x => x.RoomId == room.Id && x.State == (int)SessionState.Terminated)))
            .OrderBy(x => x.Name).Take(100).ToArrayAsync(cancellationToken);
        var activeSessionId = activeSession?.Id ?? Guid.Empty;

        return new(Audience(actor),
            await exams.CountAsync(x => x.State == (int)ExamState.Open, cancellationToken),
            await exams.CountAsync(x => x.State == (int)ExamState.Scheduled, cancellationToken),
            await sessions.CountAsync(x => x.State == (int)SessionState.Active, cancellationToken),
            await sessions.CountAsync(x => x.State == (int)SessionState.Disconnected, cancellationToken),
            await db.JudgeJobs.CountAsync(x => x.State == (int)JudgeJobState.Pending, cancellationToken),
            await activeWorkers.CountAsync(cancellationToken), await activeWorkers.SumAsync(x => x.Capacity, cancellationToken),
            normalUsed + referenceUsed, await warnings.CountAsync(cancellationToken),
            await sessions.CountAsync(x => x.State == (int)SessionState.Terminated, cancellationToken),
            assignedExam, assignedRoom, activeSession?.Id, schedule?.StartsAtUtc, schedule?.EndsAtUtc,
            activeSession is null ? null : ((SessionState)activeSession.State).ToString(),
            activeSession is null ? 0 : await db.Submissions.Where(x => x.SessionId == activeSession.Id).Select(x => x.ProblemId).Distinct().CountAsync(cancellationToken),
            activeSession is null ? 0 : await db.JudgeJobs.CountAsync(x =>
                db.Submissions.Where(s => s.SessionId == activeSessionId).Select(s => s.Id).Contains(x.SubmissionId) &&
                x.State < (int)JudgeJobState.Completed, cancellationToken),
            roomItems, warningItems, generations, []);
    }

    private IQueryable<Guid> AccessibleExamIds(IAdministrationActor actor)
    {
        if (actor.IsInRole(MastemisRoles.Administrator)) return db.Exams.Select(x => x.Id);
        if (actor.IsInRole(MastemisRoles.Candidate))
            return from registration in db.CandidateRegistrations join candidate in db.Candidates on registration.CandidateId equals candidate.Id where candidate.UserId == actor.UserId.Value select registration.ExamId;
        return db.ExamAssignments.Where(x => x.UserId == actor.UserId.Value).Select(x => x.ExamId);
    }

    private IQueryable<Guid> AccessibleRoomIds(IAdministrationActor actor, IQueryable<Guid> examIds) =>
        actor.IsInRole(MastemisRoles.RoomInvigilator)
            ? db.RoomAssignments.Where(x => x.UserId == actor.UserId.Value).Select(x => x.RoomId)
            : db.Rooms.Where(x => examIds.Contains(x.ExamId)).Select(x => x.Id);

    private IQueryable<Guid> AccessibleProblemIds(IAdministrationActor actor)
    {
        var authored = db.ProblemAuthorAssignments.Where(x => x.UserId == actor.UserId.Value && x.Status == 0 &&
            (x.ExpiresAtUtc == null || x.ExpiresAtUtc > clock.UtcNow)).Select(x => x.ProblemId);
        if (!actor.IsInRole(MastemisRoles.Administrator)) return authored;
        return db.ProblemDrafts.Select(x => x.Id);
    }

    private static string Audience(IAdministrationActor actor) => actor.IsInRole(MastemisRoles.Candidate) ? "Candidate"
        : actor.IsInRole(MastemisRoles.RoomInvigilator) ? "RoomInvigilator"
        : actor.IsInRole(MastemisRoles.ChiefInvigilator) ? "ChiefInvigilator"
        : actor.IsInRole(MastemisRoles.ExamManager) ? "ExamManager" : "Administrator";
    private static string StatusName(int status) => Enum.IsDefined(typeof(GenerationOperationStatus), status)
        ? ((GenerationOperationStatus)status).ToString() : "Unknown";
}
