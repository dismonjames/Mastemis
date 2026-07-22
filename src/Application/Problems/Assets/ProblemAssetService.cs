using Mastemis.Domain;

namespace Mastemis.Application.Problems.Assets;

public sealed record ProblemAsset(Guid Id, ProblemId ProblemId, string LogicalName, string ContentType,
    string Sha256, long Length, UserId CreatedBy, DateTimeOffset CreatedAtUtc);
public sealed record ProblemAssetContent(ProblemAsset Metadata, Stream Content);

public interface IProblemAssetStore
{
    Task<ProblemAsset> UploadAsync(ProblemId problemId, string logicalName, string contentType, Stream content, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProblemAsset>> ListAsync(ProblemId problemId, CancellationToken cancellationToken);
    Task<ProblemAssetContent?> OpenAsync(ProblemId problemId, Guid assetId, CancellationToken cancellationToken);
    Task DeleteAsync(ProblemId problemId, Guid assetId, CancellationToken cancellationToken);
}

public sealed class ProblemAssetService(IProblemAssetStore store, IAuthorizationService authorization)
{
    public async Task<ProblemAsset> UploadAsync(ProblemId id, string name, string contentType, Stream content, CancellationToken ct)
    { await authorization.EnsureAsync("problem.manage", id.Value, ct); return await store.UploadAsync(id, name, contentType, content, ct); }
    public async Task<IReadOnlyList<ProblemAsset>> ListAsync(ProblemId id, CancellationToken ct)
    { await authorization.EnsureAsync("problem.read", id.Value, ct); return await store.ListAsync(id, ct); }
    public async Task<ProblemAssetContent> OpenAsync(ProblemId id, Guid assetId, CancellationToken ct)
    { await authorization.EnsureAsync("problem.read", id.Value, ct); return await store.OpenAsync(id, assetId, ct) ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem asset not found."); }
    public async Task DeleteAsync(ProblemId id, Guid assetId, CancellationToken ct)
    { await authorization.EnsureAsync("problem.manage", id.Value, ct); await store.DeleteAsync(id, assetId, ct); }
}
