using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Evidence;

public sealed record EvidenceIdentifier(Guid Value);
public sealed record EvidencePackageItem(EvidenceIdentifier Id, EvidenceIdentifier ExamId, EvidenceIdentifier RoomId,
    EvidenceIdentifier CandidateId, EvidenceIdentifier SessionId, DateTimeOffset CreatedAtUtc, string? LatestChainHash);
public sealed record EvidenceTimelineItem(Guid Id, EvidenceIdentifier PackageId, int Type, DateTimeOffset ServerTimestampUtc,
    string ContentType, string? ObjectId, string ContentHash, string? PreviousChainHash, string MetadataJson);
public sealed record EvidenceAuditItem(Guid Id, EvidenceIdentifier ActorId, string Action, string ResourceId, DateTimeOffset OccurredAtUtc);

public interface IEvidenceClient
{
    Task<IReadOnlyList<EvidencePackageItem>> ListAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<EvidenceTimelineItem>> TimelineAsync(Guid packageId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EvidenceAuditItem>> AuditAsync(Guid packageId, CancellationToken cancellationToken);
}

public sealed class EvidenceClient(IApiTransport transport) : IEvidenceClient
{
    public async Task<IReadOnlyList<EvidencePackageItem>> ListAsync(CancellationToken cancellationToken)
        => await transport.GetAsync<List<EvidencePackageItem>>("/api/evidence/", cancellationToken).ConfigureAwait(false) ?? [];
    public async Task<IReadOnlyList<EvidenceTimelineItem>> TimelineAsync(Guid packageId, CancellationToken cancellationToken)
        => await transport.GetAsync<List<EvidenceTimelineItem>>($"/api/evidence/{packageId:D}/timeline", cancellationToken).ConfigureAwait(false) ?? [];
    public async Task<IReadOnlyList<EvidenceAuditItem>> AuditAsync(Guid packageId, CancellationToken cancellationToken)
        => await transport.GetAsync<List<EvidenceAuditItem>>($"/api/evidence/{packageId:D}/access-audit", cancellationToken).ConfigureAwait(false) ?? [];
}
