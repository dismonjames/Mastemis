using Mastemis.Application.Evidence;
using Mastemis.Domain;

namespace Mastemis.Server.Endpoints.Evidence;

public static class EvidenceEndpoints
{
    public static void MapEvidenceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/evidence").RequireAuthorization();
        group.MapGet("/", async (IEvidenceMetadataAccess service, CancellationToken ct) => await service.ListAsync(ct));
        group.MapGet("/{packageId:guid}", async (Guid packageId, IEvidenceMetadataAccess service, CancellationToken ct) => await service.GetAsync(new(packageId), ct));
        group.MapGet("/{packageId:guid}/timeline", async (Guid packageId, IEvidenceMetadataAccess service, CancellationToken ct) => await service.GetTimelineAsync(new(packageId), ct));
        group.MapGet("/{packageId:guid}/items/{itemId:guid}", async (Guid packageId, Guid itemId, IEvidenceMetadataAccess service, CancellationToken ct) => await service.GetItemAsync(new(packageId), itemId, ct));
        group.MapGet("/{packageId:guid}/access-audit", async (Guid packageId, IEvidenceMetadataAccess service, CancellationToken ct) => await service.ListAccessAuditAsync(new(packageId), ct));
        group.MapPut("/{packageId:guid}/grants/{reviewerId:guid}", async (Guid packageId, Guid reviewerId, IEvidenceMetadataAccess service, CancellationToken ct) => { await service.GrantAsync(new(packageId), new(reviewerId), ct); return Results.NoContent(); });
        group.MapDelete("/{packageId:guid}/grants/{reviewerId:guid}", async (Guid packageId, Guid reviewerId, IEvidenceMetadataAccess service, CancellationToken ct) => { await service.RevokeAsync(new(packageId), new(reviewerId), ct); return Results.NoContent(); });
    }
}
