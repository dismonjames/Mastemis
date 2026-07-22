using Mastemis.Domain;

namespace Mastemis.Application.Evidence;

public sealed record EvidenceItemMetadata(Guid Id, EvidencePackageId PackageId, EvidenceItemType Type,
    DateTimeOffset ServerTimestampUtc, string ContentType, string? ObjectId, string ContentHash,
    string? PreviousChainHash, string MetadataJson);

public sealed record EvidenceAccessAudit(Guid Id, UserId ActorId, string Action, string ResourceId,
    DateTimeOffset OccurredAtUtc);

public interface IEvidenceActor
{
    UserId UserId { get; }
    bool IsInRole(string role);
}

public interface IEvidenceMetadataAccess
{
    Task GrantAsync(EvidencePackageId packageId, UserId reviewerId, CancellationToken cancellationToken);
    Task RevokeAsync(EvidencePackageId packageId, UserId reviewerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EvidencePackageMetadata>> ListAsync(CancellationToken cancellationToken);
    Task<EvidencePackageMetadata> GetAsync(EvidencePackageId packageId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EvidenceItemMetadata>> GetTimelineAsync(EvidencePackageId packageId, CancellationToken cancellationToken);
    Task<EvidenceItemMetadata> GetItemAsync(EvidencePackageId packageId, Guid itemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EvidenceAccessAudit>> ListAccessAuditAsync(EvidencePackageId packageId, CancellationToken cancellationToken);
}
