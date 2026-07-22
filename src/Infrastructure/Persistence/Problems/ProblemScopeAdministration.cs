using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Application.Problems.Authorization;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class ProblemScopeAdministration(MastemisDbContext db, IAdministrationActor actor, IClock clock)
    : IProblemScopeAdministration
{
    public async Task<ProblemAuthorAssignment> AssignAuthorAsync(ProblemId problemId, UserId userId, ProblemAuthorRole role,
        DateTimeOffset? expiresAtUtc, CancellationToken cancellationToken)
    {
        await EnsureMayAssignAsync(problemId, role, cancellationToken);
        if (userId == actor.UserId && role == ProblemAuthorRole.Owner && !await IsOwnerAsync(problemId, actor.UserId, cancellationToken))
            throw Forbidden();
        if (expiresAtUtc <= clock.UtcNow) throw new ApplicationFailure(ErrorCodes.InvalidInput, "Assignment expiry must be in the future.");
        var row = await db.ProblemAuthorAssignments.SingleOrDefaultAsync(x => x.ProblemId == problemId.Value && x.UserId == userId.Value, cancellationToken);
        if (row is null)
        {
            row = new() { ProblemId = problemId.Value, UserId = userId.Value };
            db.ProblemAuthorAssignments.Add(row);
        }
        row.Role = (int)role; row.Status = (int)ProblemAssignmentStatus.Active; row.AssignedByUserId = actor.UserId.Value;
        row.AssignedAtUtc = clock.UtcNow; row.ExpiresAtUtc = expiresAtUtc;
        await db.SaveChangesAsync(cancellationToken); return Map(row);
    }

    public async Task RevokeAuthorAsync(ProblemId problemId, UserId userId, CancellationToken cancellationToken)
    {
        await EnsureMayAssignAsync(problemId, ProblemAuthorRole.Editor, cancellationToken);
        var row = await db.ProblemAuthorAssignments.SingleOrDefaultAsync(x => x.ProblemId == problemId.Value && x.UserId == userId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem author assignment not found.");
        if (row.Role == (int)ProblemAuthorRole.Owner && await db.ProblemAuthorAssignments.CountAsync(x => x.ProblemId == problemId.Value && x.Role == (int)ProblemAuthorRole.Owner && x.Status == 0, cancellationToken) == 1)
            throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "The last problem owner cannot be revoked.");
        row.Status = (int)ProblemAssignmentStatus.Revoked; await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProblemAuthorAssignment>> ListAuthorsAsync(ProblemId problemId, CancellationToken cancellationToken)
    {
        await EnsureCanReadAsync(problemId, cancellationToken);
        return await db.ProblemAuthorAssignments.AsNoTracking().Where(x => x.ProblemId == problemId.Value)
            .OrderBy(x => x.AssignedAtUtc).Select(x => Map(x)).ToArrayAsync(cancellationToken);
    }

    public async Task AssignExamAsync(ProblemId problemId, ExamId examId, CancellationToken cancellationToken)
    {
        await EnsureExamManagerAsync(examId, cancellationToken);
        if (!await db.ProblemDrafts.AnyAsync(x => x.Id == problemId.Value, cancellationToken)) throw new ApplicationFailure(ErrorCodes.NotFound, "Problem not found.");
        if (!await db.ExamProblemAssignments.AnyAsync(x => x.ExamId == examId.Value && x.ProblemId == problemId.Value, cancellationToken))
        { db.ExamProblemAssignments.Add(new() { ExamId = examId.Value, ProblemId = problemId.Value, AssignedByUserId = actor.UserId.Value, AssignedAtUtc = clock.UtcNow }); await db.SaveChangesAsync(cancellationToken); }
    }

    public async Task RemoveExamAsync(ProblemId problemId, ExamId examId, CancellationToken cancellationToken)
    {
        await EnsureExamManagerAsync(examId, cancellationToken);
        var row = await db.ExamProblemAssignments.SingleOrDefaultAsync(x => x.ExamId == examId.Value && x.ProblemId == problemId.Value, cancellationToken);
        if (row is not null) { db.ExamProblemAssignments.Remove(row); await db.SaveChangesAsync(cancellationToken); }
    }

    private async Task EnsureMayAssignAsync(ProblemId id, ProblemAuthorRole role, CancellationToken ct)
    {
        if (await IsOwnerAsync(id, actor.UserId, ct)) return;
        if (actor.IsInRole(MastemisRoles.ExamManager) && await HasManagedExamAsync(id, ct) && role != ProblemAuthorRole.Owner) return;
        throw Forbidden();
    }
    private async Task EnsureCanReadAsync(ProblemId id, CancellationToken ct)
    { if (!await HasActiveAssignmentAsync(id, actor.UserId, ct) && !await HasManagedExamAsync(id, ct)) throw Forbidden(); }
    private Task<bool> IsOwnerAsync(ProblemId id, UserId user, CancellationToken ct) => db.ProblemAuthorAssignments.AnyAsync(x => x.ProblemId == id.Value && x.UserId == user.Value && x.Role == 0 && x.Status == 0 && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > clock.UtcNow), ct);
    private Task<bool> HasActiveAssignmentAsync(ProblemId id, UserId user, CancellationToken ct) => db.ProblemAuthorAssignments.AnyAsync(x => x.ProblemId == id.Value && x.UserId == user.Value && x.Status == 0 && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > clock.UtcNow), ct);
    private Task<bool> HasManagedExamAsync(ProblemId id, CancellationToken ct) => (from problem in db.ExamProblemAssignments join assignment in db.ExamAssignments on problem.ExamId equals assignment.ExamId where problem.ProblemId == id.Value && assignment.UserId == actor.UserId.Value && assignment.Role == MastemisRoles.ExamManager select problem).AnyAsync(ct);
    private async Task EnsureExamManagerAsync(ExamId examId, CancellationToken ct)
    { if (!actor.IsInRole(MastemisRoles.ExamManager) || !await db.ExamAssignments.AnyAsync(x => x.ExamId == examId.Value && x.UserId == actor.UserId.Value && x.Role == MastemisRoles.ExamManager, ct)) throw Forbidden(); }
    private static ProblemAuthorAssignment Map(ProblemAuthorAssignmentRow x) => new(new(x.ProblemId), new(x.UserId), (ProblemAuthorRole)x.Role, (ProblemAssignmentStatus)x.Status, new(x.AssignedByUserId), x.AssignedAtUtc, x.ExpiresAtUtc);
    private static ApplicationFailure Forbidden() => new(ErrorCodes.Forbidden, "The current identity cannot manage this problem scope.");
}
