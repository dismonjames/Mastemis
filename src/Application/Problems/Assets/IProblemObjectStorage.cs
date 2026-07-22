namespace Mastemis.Application.Problems.Assets;

public enum ProblemObjectKind
{
    Asset,
    Statement,
    Package,
    TestInput,
    ExpectedOutput,
    ReferenceSource,
    Export
}

public sealed record StagedProblemObject(string ObjectId, string Sha256, long Length, DateTimeOffset StagedAtUtc);

public interface IProblemObjectStorage
{
    Task<StagedProblemObject> StageAsync(ProblemObjectKind kind, Stream content, long maximumBytes,
        CancellationToken cancellationToken);

    Task MarkReferencedAsync(string objectId, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string objectId, long maximumBytes, CancellationToken cancellationToken);

    Task DeleteStagedAsync(string objectId, CancellationToken cancellationToken);
    Task DeleteReferencedAsync(string objectId, CancellationToken cancellationToken);
}

public interface IProblemObjectReferenceLookup
{
    Task<IReadOnlySet<string>> FindReferencedAsync(IReadOnlyCollection<string> objectIds,
        CancellationToken cancellationToken);
}
