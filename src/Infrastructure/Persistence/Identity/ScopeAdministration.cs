using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Identity;

public sealed class ScopeAdministration(MastemisDbContext db, IAdministrationActor actor, IClock clock)
    : IScopeAdministration
{
    public async Task<ScopeAssignment> AssignExamAsync(ExamId examId, UserId userId, string role, CancellationToken cancellationToken)
    {
        await EnsureExamManagerAsync(examId, cancellationToken); EnsureNotSelf(userId); ValidateExamRole(role);
        _ = await db.Exams.SingleOrDefaultAsync(x => x.Id == examId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Exam not found.");
        var existing = await db.ExamAssignments.SingleOrDefaultAsync(x => x.ExamId == examId.Value && x.UserId == userId.Value && x.Role == role, cancellationToken);
        if (existing is null) { existing = new ExamAssignmentRow { ExamId = examId.Value, UserId = userId.Value, Role = role, AssignedAtUtc = clock.UtcNow }; db.ExamAssignments.Add(existing); await db.SaveChangesAsync(cancellationToken); }
        return Map(existing);
    }

    public async Task RemoveExamAsync(ExamId examId, UserId userId, string role, CancellationToken cancellationToken)
    {
        await EnsureExamManagerAsync(examId, cancellationToken); EnsureNotSelf(userId); ValidateExamRole(role);
        var row = await db.ExamAssignments.SingleOrDefaultAsync(x => x.ExamId == examId.Value && x.UserId == userId.Value && x.Role == role, cancellationToken);
        if (row is not null) { db.ExamAssignments.Remove(row); await db.SaveChangesAsync(cancellationToken); }
    }

    public async Task<IReadOnlyList<ScopeAssignment>> ListExamAsync(ExamId examId, CancellationToken cancellationToken)
    {
        await EnsureExamManagerAsync(examId, cancellationToken);
        return (await db.ExamAssignments.AsNoTracking().Where(x => x.ExamId == examId.Value).ToListAsync(cancellationToken)).Select(Map).ToArray();
    }

    public async Task<ScopeAssignment> AssignRoomAsync(RoomId roomId, UserId userId, CancellationToken cancellationToken)
    {
        EnsureNotSelf(userId);
        var room = await db.Rooms.SingleOrDefaultAsync(x => x.Id == roomId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Room not found.");
        await EnsureExamManagerAsync(new(room.ExamId), cancellationToken);
        var existing = await db.RoomAssignments.SingleOrDefaultAsync(x => x.RoomId == roomId.Value && x.UserId == userId.Value, cancellationToken);
        if (existing is null) { existing = new RoomAssignmentRow { RoomId = roomId.Value, UserId = userId.Value, AssignedAtUtc = clock.UtcNow }; db.RoomAssignments.Add(existing); await db.SaveChangesAsync(cancellationToken); }
        return new(roomId.Value, userId, MastemisRoles.RoomInvigilator, existing.AssignedAtUtc);
    }

    public async Task RemoveRoomAsync(RoomId roomId, UserId userId, CancellationToken cancellationToken)
    {
        EnsureNotSelf(userId);
        var room = await db.Rooms.SingleOrDefaultAsync(x => x.Id == roomId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Room not found.");
        await EnsureExamManagerAsync(new(room.ExamId), cancellationToken);
        var row = await db.RoomAssignments.SingleOrDefaultAsync(x => x.RoomId == roomId.Value && x.UserId == userId.Value, cancellationToken);
        if (row is not null) { db.RoomAssignments.Remove(row); await db.SaveChangesAsync(cancellationToken); }
    }

    public async Task<IReadOnlyList<ScopeAssignment>> ListRoomAsync(RoomId roomId, CancellationToken cancellationToken)
    {
        var room = await db.Rooms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == roomId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Room not found.");
        await EnsureExamManagerAsync(new(room.ExamId), cancellationToken);
        return (await db.RoomAssignments.AsNoTracking().Where(x => x.RoomId == roomId.Value).ToListAsync(cancellationToken))
            .Select(x => new ScopeAssignment(roomId.Value, new(x.UserId), MastemisRoles.RoomInvigilator, x.AssignedAtUtc)).ToArray();
    }

    private async Task EnsureExamManagerAsync(ExamId examId, CancellationToken ct)
    {
        if (actor.IsInRole(MastemisRoles.Administrator)) return;
        if (!actor.IsInRole(MastemisRoles.ExamManager) || !await db.ExamAssignments.AnyAsync(x => x.ExamId == examId.Value && x.UserId == actor.UserId.Value && x.Role == MastemisRoles.ExamManager, ct))
            throw new ApplicationFailure(ErrorCodes.Forbidden, "The current identity cannot manage this examination scope.");
    }
    private void EnsureNotSelf(UserId target) { if (target == actor.UserId) throw new ApplicationFailure(ErrorCodes.Forbidden, "Self-escalation is not allowed."); }
    private static void ValidateExamRole(string role) { if (role is not (MastemisRoles.ExamManager or MastemisRoles.ChiefInvigilator)) throw new ApplicationFailure(ErrorCodes.InvalidInput, "Role is not valid for examination scope."); }
    private static ScopeAssignment Map(ExamAssignmentRow row) => new(row.ExamId, new(row.UserId), row.Role, row.AssignedAtUtc);
}
