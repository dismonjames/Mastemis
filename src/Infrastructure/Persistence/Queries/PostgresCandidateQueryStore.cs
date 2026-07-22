using Mastemis.Application.Candidates.Queries;
using Mastemis.Application.Queries;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Queries;

public sealed class PostgresCandidateQueryStore(MastemisDbContext db) : ICandidateQueryStore
{
    public async Task<PagedResult<CandidateListItem>> ListAsync(Guid examId, CandidateListQuery request,
        CancellationToken cancellationToken)
    {
        var query = Base(examId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.User.UserName!, $"%{term}%") ||
                EF.Functions.ILike(x.User.DisplayName, $"%{term}%") || EF.Functions.ILike(x.Registration.RegistrationCode, $"%{term}%"));
        }
        if (!string.IsNullOrWhiteSpace(request.AccessState) && Enum.TryParse<CandidateExamAccessState>(request.AccessState, true, out var access))
            query = query.Where(x => x.Registration.AccessState == (int)access);
        if (request.RoomId is { } roomId) query = query.Where(x => x.Session != null && x.Session.RoomId == roomId);
        if (!string.IsNullOrWhiteSpace(request.SessionState) && Enum.TryParse<SessionState>(request.SessionState, true, out var session))
            query = query.Where(x => x.Session != null && x.Session.State == (int)session);
        var total = await query.CountAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var items = await query.OrderBy(x => x.User.DisplayName).Skip(request.Offset).Take(request.Limit)
            .Select(x => new CandidateListItem(x.Candidate.Id, x.Candidate.UserId, x.User.UserName ?? string.Empty,
                x.User.DisplayName, x.User.LockoutEnd == null || x.User.LockoutEnd <= now,
                x.Registration.RegistrationCode, ((CandidateExamAccessState)x.Registration.AccessState).ToString(),
                x.Session == null ? null : x.Session.Id,
                x.Session == null ? null : ((SessionState)x.Session.State).ToString(),
                x.Session == null ? null : x.Session.RoomId,
                db.ConfirmedWarnings.Count(w => w.ExamId == examId && w.CandidateId == x.Candidate.Id),
                x.Session == null ? null : x.Session.TerminatedAtUtc ?? x.Session.StartedAtUtc))
            .ToArrayAsync(cancellationToken);
        return new(items, request.Offset, request.Limit, total);
    }

    public async Task<CandidateListItem?> GetAsync(Guid examId, Guid candidateId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return await Base(examId).Where(x => x.Candidate.Id == candidateId)
            .Select(x => new CandidateListItem(x.Candidate.Id, x.Candidate.UserId, x.User.UserName ?? string.Empty,
                x.User.DisplayName, x.User.LockoutEnd == null || x.User.LockoutEnd <= now,
                x.Registration.RegistrationCode, ((CandidateExamAccessState)x.Registration.AccessState).ToString(),
                x.Session == null ? null : x.Session.Id,
                x.Session == null ? null : ((SessionState)x.Session.State).ToString(),
                x.Session == null ? null : x.Session.RoomId,
                db.ConfirmedWarnings.Count(w => w.ExamId == examId && w.CandidateId == x.Candidate.Id),
                x.Session == null ? null : x.Session.TerminatedAtUtc ?? x.Session.StartedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private IQueryable<CandidateProjection> Base(Guid examId) =>
        from registration in db.CandidateRegistrations.AsNoTracking()
        join candidate in db.Candidates.AsNoTracking() on registration.CandidateId equals candidate.Id
        join user in db.Users.AsNoTracking() on candidate.UserId equals user.Id
        where registration.ExamId == examId
        let session = db.ExamSessions.Where(x => x.ExamId == examId && x.CandidateId == candidate.Id)
            .OrderByDescending(x => x.StartedAtUtc).FirstOrDefault()
        select new CandidateProjection(registration, candidate, user, session);

    private sealed record CandidateProjection(CandidateRegistrationRow Registration, CandidateRow Candidate,
        ApplicationUser User, SessionRow? Session);
}
