using Mastemis.Domain;

namespace Mastemis.Application.Problems.ReferenceOutputs;

public sealed record ReferenceSolutionSourceInput(string FileName, ReadOnlyMemory<byte> Content);
public sealed record ReferenceSolutionSourceMetadata(string FileName, string Sha256, long Length);
public sealed record ReferenceSolutionRevision(Guid RevisionId, ProblemId ProblemId, string Language,
    IReadOnlyList<ReferenceSolutionSourceMetadata> Sources, UserId CreatedBy, DateTimeOffset CreatedAtUtc, bool Enabled);
public sealed record ReferenceSolutionSourceContent(string FileName, string Sha256, Stream Content);

public interface IReferenceSolutionStore
{
    Task<ReferenceSolutionRevision> SaveAsync(ProblemId problemId, string language, IReadOnlyList<ReferenceSolutionSourceInput> sources, CancellationToken cancellationToken);
    Task<ReferenceSolutionRevision?> GetCurrentAsync(ProblemId problemId, CancellationToken cancellationToken);
    Task<ReferenceSolutionSourceContent?> OpenSourceAsync(ProblemId problemId, Guid revisionId, string fileName, CancellationToken cancellationToken);
}

public sealed class ReferenceSolutionService(IReferenceSolutionStore store, IAuthorizationService authorization)
{
    public async Task<ReferenceSolutionRevision> SaveAsync(ProblemId id, string language, IReadOnlyList<ReferenceSolutionSourceInput> sources, CancellationToken ct)
    { await authorization.EnsureAsync("problem.manage", id.Value, ct); return await store.SaveAsync(id, language, sources, ct); }
    public async Task<ReferenceSolutionRevision> GetAsync(ProblemId id, CancellationToken ct)
    { await authorization.EnsureAsync("problem.read", id.Value, ct); return await store.GetCurrentAsync(id, ct) ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Reference solution not found."); }
    public async Task<ReferenceSolutionSourceContent> OpenSourceAsync(ProblemId id, Guid revisionId, string fileName, CancellationToken ct)
    { await authorization.EnsureAsync("problem.hidden", id.Value, ct); return await store.OpenSourceAsync(id, revisionId, fileName, ct) ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Reference source not found."); }
}
