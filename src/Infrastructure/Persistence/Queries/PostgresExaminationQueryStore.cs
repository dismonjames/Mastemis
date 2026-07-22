using Mastemis.Application.Administration;
using Mastemis.Application.Examinations.Queries;
using Mastemis.Application.Queries;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Queries;

public sealed class PostgresExaminationQueryStore(MastemisDbContext db) : IExaminationQueryStore
{
    public async Task<PagedResult<ExaminationListItem>> ListAsync(IAdministrationActor actor, ExaminationListQuery request,
        CancellationToken cancellationToken)
    {
        var accessible = AccessibleExamIds(actor);
        var query = db.Exams.AsNoTracking().Where(x => accessible.Contains(x.Id));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Title, $"%{term}%"));
        }
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ExamState>(request.Status, true, out var state))
            query = query.Where(x => x.State == (int)state);
        if (request.FromUtc is { } from) query = query.Where(x => x.EndsAtUtc == null || x.EndsAtUtc >= from);
        if (request.ToUtc is { } to) query = query.Where(x => x.StartsAtUtc == null || x.StartsAtUtc <= to);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).Skip(request.Offset).Take(request.Limit)
            .Select(x => new ExaminationListItem(x.Id, x.Title, ((ExamState)x.State).ToString(), x.CreatedAtUtc,
                x.StartsAtUtc, x.EndsAtUtc, db.Rooms.Count(r => r.ExamId == x.Id),
                db.CandidateRegistrations.Count(r => r.ExamId == x.Id),
                db.ExamSessions.Count(s => s.ExamId == x.Id && s.State == (int)SessionState.Active),
                db.ConfirmedWarnings.Count(w => w.ExamId == x.Id))).ToArrayAsync(cancellationToken);
        return new(items, request.Offset, request.Limit, total);
    }

    public async Task<ExaminationDetails?> GetAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await db.Exams.AsNoTracking().Where(x => x.Id == examId)
            .Select(x => new ExaminationListItem(x.Id, x.Title, ((ExamState)x.State).ToString(), x.CreatedAtUtc,
                x.StartsAtUtc, x.EndsAtUtc, db.Rooms.Count(r => r.ExamId == x.Id),
                db.CandidateRegistrations.Count(r => r.ExamId == x.Id),
                db.ExamSessions.Count(s => s.ExamId == x.Id && s.State == (int)SessionState.Active),
                db.ConfirmedWarnings.Count(w => w.ExamId == x.Id))).SingleOrDefaultAsync(cancellationToken);
        if (exam is null) return null;
        var rooms = await db.Rooms.AsNoTracking().Where(x => x.ExamId == examId).OrderBy(x => x.Name)
            .Select(x => new ExaminationRoomSummary(x.Id, x.Code, x.Name,
                db.ExamSessions.Where(s => s.RoomId == x.Id).Select(s => s.CandidateId).Distinct().Count(),
                db.ExamSessions.Count(s => s.RoomId == x.Id && s.State == (int)SessionState.Active),
                db.ExamSessions.Count(s => s.RoomId == x.Id && s.State == (int)SessionState.Disconnected),
                db.ConfirmedWarnings.Count(w => w.RoomId == x.Id))).ToArrayAsync(cancellationToken);
        var candidates = await (from registration in db.CandidateRegistrations.AsNoTracking()
                                join candidate in db.Candidates.AsNoTracking() on registration.CandidateId equals candidate.Id
                                join user in db.Users.AsNoTracking() on candidate.UserId equals user.Id
                                where registration.ExamId == examId
                                let session = db.ExamSessions.Where(x => x.ExamId == examId && x.CandidateId == candidate.Id)
                                    .OrderByDescending(x => x.StartedAtUtc).FirstOrDefault()
                                orderby user.DisplayName
                                select new ExaminationCandidateSummary(candidate.Id, candidate.UserId, user.DisplayName,
                                    ((CandidateExamAccessState)registration.AccessState).ToString(),
                                    session == null ? null : ((SessionState)session.State).ToString(), session == null ? null : session.RoomId,
                                    db.ConfirmedWarnings.Count(x => x.ExamId == examId && x.CandidateId == candidate.Id)))
            .Take(500).ToArrayAsync(cancellationToken);
        var sessions = await db.ExamSessions.AsNoTracking().Where(x => x.ExamId == examId).OrderByDescending(x => x.StartedAtUtc)
            .Select(x => new ExaminationSessionSummary(x.Id, x.CandidateId, x.RoomId, ((SessionState)x.State).ToString(),
                x.StartedAtUtc, x.TerminatedAtUtc, db.ConfirmedWarnings.Count(w => w.SessionId == x.Id)))
            .Take(500).ToArrayAsync(cancellationToken);
        var problems = await (from assignment in db.ExamProblemAssignments.AsNoTracking()
                              join problem in db.ProblemDrafts.AsNoTracking() on assignment.ProblemId equals problem.Id
                              where assignment.ExamId == examId orderby problem.Title
                              select new ExaminationProblemSummary(problem.Id, problem.Title, problem.Version, assignment.AssignedAtUtc))
            .Take(200).ToArrayAsync(cancellationToken);
        var timeline = new List<ExaminationTimelineItem> { new("Created", exam.CreatedAtUtc, "Examination draft created") };
        if (exam.StartsAtUtc is { } starts) timeline.Add(new("ScheduledStart", starts, "Scheduled start"));
        if (exam.EndsAtUtc is { } ends) timeline.Add(new("ScheduledEnd", ends, "Scheduled end"));
        timeline.AddRange(await db.AuditRecords.AsNoTracking().Where(x => x.ResourceId == examId.ToString())
            .OrderByDescending(x => x.OccurredAtUtc).Take(50)
            .Select(x => new ExaminationTimelineItem(x.Action, x.OccurredAtUtc, x.Action)).ToArrayAsync(cancellationToken));
        return new(exam, rooms, candidates, sessions, problems, timeline.OrderByDescending(x => x.OccurredAtUtc).ToArray());
    }

    private IQueryable<Guid> AccessibleExamIds(IAdministrationActor actor)
    {
        if (actor.IsInRole(MastemisRoles.Administrator)) return db.Exams.Select(x => x.Id);
        if (actor.IsInRole(MastemisRoles.Candidate))
            return from registration in db.CandidateRegistrations join candidate in db.Candidates on registration.CandidateId equals candidate.Id where candidate.UserId == actor.UserId.Value select registration.ExamId;
        return db.ExamAssignments.Where(x => x.UserId == actor.UserId.Value).Select(x => x.ExamId);
    }
}
