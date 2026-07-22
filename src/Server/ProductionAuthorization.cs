using System.Security.Claims;
using Mastemis.Application;
using Mastemis.Domain;
using Mastemis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class ProductionApplicationAuthorization(IHttpContextAccessor accessor, MastemisDbContext db, IClock clock)
    : Mastemis.Application.IAuthorizationService
{
    public async ValueTask EnsureAsync(string permission, Guid scopeId, CancellationToken cancellationToken)
    {
        var principal = accessor.HttpContext?.User;
        var userIdText = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (principal?.Identity?.IsAuthenticated != true || !Guid.TryParse(userIdText, out var userId))
            throw new ApplicationFailure(ErrorCodes.Forbidden, "The current identity is not authorized for this resource.");
        var roles = principal!.FindAll(ClaimTypes.Role).Select(x => x.Value).ToHashSet(StringComparer.Ordinal);
        if (roles.Contains(MastemisRoles.JudgeWorker)) Denied();

        var allowed = permission switch
        {
            "exam.create" => HasAny(roles, MastemisRoles.Administrator, MastemisRoles.ExamManager),
            "problem.create" => HasAny(roles, MastemisRoles.Administrator, MastemisRoles.ExamManager),
            "problem.manage" => await CanMutateProblemAsync(scopeId, userId, roles, cancellationToken),
            "problem.read" => await CanReadProblemAsync(scopeId, userId, roles, cancellationToken),
            "problem.hidden" => await HasProblemRoleAsync(scopeId, userId, 0, 1, cancellationToken),
            "exam.manage" or "room.create" or "candidate.register" =>
                HasAny(roles, MastemisRoles.Administrator, MastemisRoles.ExamManager) ||
                await HasExamAssignmentAsync(scopeId, userId, MastemisRoles.ExamManager, cancellationToken),
            "warning.issue" or "sfe.evaluate" => roles.Contains(MastemisRoles.ChiefInvigilator) &&
                await HasSessionExamAssignmentAsync(scopeId, userId, MastemisRoles.ChiefInvigilator, cancellationToken),
            "exam.realtime" => await CanAccessExamAsync(scopeId, userId, roles, cancellationToken),
            "chief.realtime" => roles.Contains(MastemisRoles.ChiefInvigilator) &&
                await HasExamAssignmentAsync(scopeId, userId, MastemisRoles.ChiefInvigilator, cancellationToken),
            "room.realtime" => await CanAccessRoomAsync(scopeId, userId, roles, cancellationToken),
            "candidate.realtime" => roles.Contains(MastemisRoles.Candidate) &&
                await CanAccessCandidateRealtimeAsync(scopeId, userId, cancellationToken),
            "session.start" => await OwnsCandidateAsync(scopeId, userId, cancellationToken),
            "session.access" or "session.write" or "submission.create" or "sfe.record" =>
                await CanAccessSessionAsync(scopeId, userId, roles, cancellationToken),
            "submission.read" => await CanAccessSubmissionAsync(scopeId, userId, roles, cancellationToken),
            "evidence.read" => roles.Contains(MastemisRoles.EvidenceReviewer),
            "audit.read" => roles.Contains(MastemisRoles.Administrator),
            _ => false
        };
        if (!allowed) Denied();
    }

    private Task<bool> HasExamAssignmentAsync(Guid examId, Guid userId, string role, CancellationToken ct) =>
        db.ExamAssignments.AnyAsync(x => x.ExamId == examId && x.UserId == userId && x.Role == role, ct);
    private async Task<bool> HasSessionExamAssignmentAsync(Guid sessionId, Guid userId, string role, CancellationToken ct)
    {
        var examId = await db.ExamSessions.Where(x => x.Id == sessionId).Select(x => (Guid?)x.ExamId).SingleOrDefaultAsync(ct);
        return examId is { } id && await HasExamAssignmentAsync(id, userId, role, ct);
    }
    private async Task<bool> CanAccessExamAsync(Guid examId, Guid userId, HashSet<string> roles, CancellationToken ct) =>
        HasAny(roles, MastemisRoles.Administrator, MastemisRoles.ExamManager) ||
        await db.ExamAssignments.AnyAsync(x => x.ExamId == examId && x.UserId == userId, ct);
    private async Task<bool> CanAccessRoomAsync(Guid roomId, Guid userId, HashSet<string> roles, CancellationToken ct)
    {
        if (HasAny(roles, MastemisRoles.Administrator, MastemisRoles.ExamManager)) return true;
        if (roles.Contains(MastemisRoles.RoomInvigilator) && await db.RoomAssignments.AnyAsync(x => x.RoomId == roomId && x.UserId == userId, ct)) return true;
        var examId = await db.Rooms.Where(x => x.Id == roomId).Select(x => (Guid?)x.ExamId).SingleOrDefaultAsync(ct);
        return examId is { } id && roles.Contains(MastemisRoles.ChiefInvigilator) && await HasExamAssignmentAsync(id, userId, MastemisRoles.ChiefInvigilator, ct);
    }
    private Task<bool> OwnsCandidateAsync(Guid candidateId, Guid userId, CancellationToken ct) =>
        db.Candidates.AnyAsync(x => x.Id == candidateId && x.UserId == userId, ct);
    private Task<bool> CanAccessCandidateRealtimeAsync(Guid candidateId, Guid userId, CancellationToken ct) =>
        (from candidate in db.Candidates
         join registration in db.CandidateRegistrations on candidate.Id equals registration.CandidateId
         where candidate.Id == candidateId && candidate.UserId == userId && registration.AccessState == (int)CandidateExamAccessState.Enabled
         select registration).AnyAsync(ct);
    private async Task<bool> CanAccessSessionAsync(Guid sessionId, Guid userId, HashSet<string> roles, CancellationToken ct)
    {
        var data = await (from session in db.ExamSessions
                          join candidate in db.Candidates on session.CandidateId equals candidate.Id
                          join registration in db.CandidateRegistrations on new { session.ExamId, session.CandidateId } equals new { registration.ExamId, registration.CandidateId }
                          where session.Id == sessionId
                          select new { session.RoomId, session.ExamId, candidate.UserId, registration.AccessState }).SingleOrDefaultAsync(ct);
        if (data is null) return false;
        if (roles.Contains(MastemisRoles.Candidate)) return data.UserId == userId && data.AccessState == (int)CandidateExamAccessState.Enabled;
        if (roles.Contains(MastemisRoles.RoomInvigilator)) return await db.RoomAssignments.AnyAsync(x => x.RoomId == data.RoomId && x.UserId == userId, ct);
        if (roles.Contains(MastemisRoles.ChiefInvigilator)) return await HasExamAssignmentAsync(data.ExamId, userId, MastemisRoles.ChiefInvigilator, ct);
        return HasAny(roles, MastemisRoles.Administrator, MastemisRoles.ExamManager);
    }
    private async Task<bool> CanAccessSubmissionAsync(Guid submissionId, Guid userId, HashSet<string> roles, CancellationToken ct)
    {
        var sessionId = await db.Submissions.Where(x => x.Id == submissionId).Select(x => (Guid?)x.SessionId).SingleOrDefaultAsync(ct);
        return sessionId is { } id && await CanAccessSessionAsync(id, userId, roles, ct);
    }
    private async Task<bool> CanMutateProblemAsync(Guid problemId, Guid userId, HashSet<string> roles, CancellationToken ct)
    {
        if (await db.ExamProblemAssignments.Where(x => x.ProblemId == problemId)
            .Join(db.Exams, x => x.ExamId, x => x.Id, (_, exam) => exam.State).AnyAsync(x => x == (int)ExamState.Open, ct)) return false;
        if (await HasProblemRoleAsync(problemId, userId, 0, 1, ct)) return true;
        return roles.Contains(MastemisRoles.ExamManager) && await (from problem in db.ExamProblemAssignments
                                                                   join assignment in db.ExamAssignments on problem.ExamId equals assignment.ExamId
                                                                   where problem.ProblemId == problemId && assignment.UserId == userId && assignment.Role == MastemisRoles.ExamManager
                                                                   select problem).AnyAsync(ct);
    }
    private async Task<bool> CanReadProblemAsync(Guid problemId, Guid userId, HashSet<string> roles, CancellationToken ct) =>
        await db.ProblemAuthorAssignments.AnyAsync(x => x.ProblemId == problemId && x.UserId == userId && x.Status == 0 && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > clock.UtcNow), ct) ||
        roles.Contains(MastemisRoles.ExamManager) && await (from problem in db.ExamProblemAssignments join assignment in db.ExamAssignments on problem.ExamId equals assignment.ExamId where problem.ProblemId == problemId && assignment.UserId == userId && assignment.Role == MastemisRoles.ExamManager select problem).AnyAsync(ct);
    private Task<bool> HasProblemRoleAsync(Guid problemId, Guid userId, int first, int second, CancellationToken ct) =>
        db.ProblemAuthorAssignments.AnyAsync(x => x.ProblemId == problemId && x.UserId == userId && x.Status == 0 &&
            (x.Role == first || x.Role == second) && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > clock.UtcNow), ct);
    private static bool HasAny(HashSet<string> roles, params string[] expected) => expected.Any(roles.Contains);
    private static void Denied() => throw new ApplicationFailure(ErrorCodes.Forbidden, "The current identity is not authorized for this resource.");
}
