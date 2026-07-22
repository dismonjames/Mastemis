using Mastemis.Application;
using Mastemis.Application.Evidence;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Auditing;

public sealed class EvidenceMetadataAccess(MastemisDbContext db, IEvidenceActor actor, IClock clock)
    : IEvidenceMetadataAccess
{
    public async Task GrantAsync(EvidencePackageId packageId, UserId reviewerId, CancellationToken cancellationToken)
    {
        EnsureAdministrator();
        _ = await RequiredPackageAsync(packageId, cancellationToken);
        if (!await db.EvidenceReviewGrants.AnyAsync(x => x.PackageId == packageId.Value && x.ReviewerId == reviewerId.Value, cancellationToken))
        {
            db.EvidenceReviewGrants.Add(new EvidenceReviewGrantRow
            {
                PackageId = packageId.Value,
                ReviewerId = reviewerId.Value,
                GrantedByUserId = actor.UserId.Value,
                GrantedAtUtc = clock.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAsync(EvidencePackageId packageId, UserId reviewerId, CancellationToken cancellationToken)
    {
        EnsureAdministrator();
        var row = await db.EvidenceReviewGrants.SingleOrDefaultAsync(x => x.PackageId == packageId.Value && x.ReviewerId == reviewerId.Value, cancellationToken);
        if (row is not null) { db.EvidenceReviewGrants.Remove(row); await db.SaveChangesAsync(cancellationToken); }
    }

    public async Task<IReadOnlyList<EvidencePackageMetadata>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await db.EvidencePackages.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        var visible = new List<EvidencePackageMetadata>();
        foreach (var row in rows)
            if (await CanReadAsync(row, cancellationToken)) visible.Add(Map(row));
        await AuditAsync("evidence.metadata.list", "visible-packages", cancellationToken);
        return visible;
    }

    public async Task<EvidencePackageMetadata> GetAsync(EvidencePackageId packageId, CancellationToken cancellationToken)
    {
        var row = await RequiredAuthorizedPackageAsync(packageId, cancellationToken);
        await AuditAsync("evidence.metadata.read", packageId.Value.ToString("D"), cancellationToken);
        return Map(row);
    }

    public async Task<IReadOnlyList<EvidenceItemMetadata>> GetTimelineAsync(EvidencePackageId packageId, CancellationToken cancellationToken)
    {
        _ = await RequiredAuthorizedPackageAsync(packageId, cancellationToken);
        var rows = await db.EvidenceItems.AsNoTracking().Where(x => x.PackageId == packageId.Value)
            .OrderBy(x => x.ServerTimestampUtc).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        await AuditAsync("evidence.timeline.read", packageId.Value.ToString("D"), cancellationToken);
        return rows.Select(Map).ToArray();
    }

    public async Task<EvidenceItemMetadata> GetItemAsync(EvidencePackageId packageId, Guid itemId, CancellationToken cancellationToken)
    {
        _ = await RequiredAuthorizedPackageAsync(packageId, cancellationToken);
        var row = await db.EvidenceItems.AsNoTracking().SingleOrDefaultAsync(x => x.PackageId == packageId.Value && x.Id == itemId, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Evidence item not found.");
        await AuditAsync("evidence.item.read", itemId.ToString("D"), cancellationToken);
        return Map(row);
    }

    public async Task<IReadOnlyList<EvidenceAccessAudit>> ListAccessAuditAsync(EvidencePackageId packageId, CancellationToken cancellationToken)
    {
        _ = await RequiredAuthorizedPackageAsync(packageId, cancellationToken);
        var id = packageId.Value.ToString("D");
        var rows = await db.AuditRecords.AsNoTracking().Where(x => x.ResourceId == id && x.Action.StartsWith("evidence."))
            .OrderBy(x => x.OccurredAtUtc).ToListAsync(cancellationToken);
        await AuditAsync("evidence.audit.read", id, cancellationToken);
        return rows.Where(x => x.ActorUserId.HasValue).Select(x => new EvidenceAccessAudit(x.Id, new(x.ActorUserId!.Value), x.Action, x.ResourceId, x.OccurredAtUtc)).ToArray();
    }

    private async Task<EvidencePackageRow> RequiredAuthorizedPackageAsync(EvidencePackageId id, CancellationToken ct)
    {
        var row = await RequiredPackageAsync(id, ct);
        if (!await CanReadAsync(row, ct)) throw new ApplicationFailure(ErrorCodes.Forbidden, "Evidence metadata access is not granted.");
        return row;
    }
    private async Task<EvidencePackageRow> RequiredPackageAsync(EvidencePackageId id, CancellationToken ct) =>
        await db.EvidencePackages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value, ct)
        ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Evidence package not found.");
    private async Task<bool> CanReadAsync(EvidencePackageRow row, CancellationToken ct)
    {
        if (actor.IsInRole(MastemisRoles.Candidate) || actor.IsInRole(MastemisRoles.JudgeWorker)) return false;
        if (await db.EvidenceReviewGrants.AnyAsync(x => x.PackageId == row.Id && x.ReviewerId == actor.UserId.Value, ct)) return true;
        if (actor.IsInRole(MastemisRoles.RoomInvigilator) && await db.RoomAssignments.AnyAsync(x => x.RoomId == row.RoomId && x.UserId == actor.UserId.Value, ct)) return true;
        return actor.IsInRole(MastemisRoles.ChiefInvigilator) && await db.ExamAssignments.AnyAsync(x => x.ExamId == row.ExamId && x.UserId == actor.UserId.Value && x.Role == MastemisRoles.ChiefInvigilator, ct);
    }
    private async Task AuditAsync(string action, string resourceId, CancellationToken ct)
    {
        db.AuditRecords.Add(new AuditRow
        {
            Id = Guid.NewGuid(),
            ActorUserId = actor.UserId.Value,
            Action = action,
            ResourceType = "evidence",
            ResourceId = resourceId,
            OccurredAtUtc = clock.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
    private void EnsureAdministrator() { if (!actor.IsInRole(MastemisRoles.Administrator)) throw new ApplicationFailure(ErrorCodes.Forbidden, "Only administrators may manage evidence review grants."); }
    private static EvidencePackageMetadata Map(EvidencePackageRow x) => new(new(x.Id), new(x.ExamId), new(x.RoomId), new(x.CandidateId), new(x.SessionId), x.CreatedAtUtc, x.LatestChainHash);
    private static EvidenceItemMetadata Map(EvidenceItemRow x) => new(x.Id, new(x.PackageId), (EvidenceItemType)x.Type, x.ServerTimestampUtc, x.ContentType, x.ObjectId, x.ContentHash, x.PreviousChainHash, x.MetadataJson);
}
