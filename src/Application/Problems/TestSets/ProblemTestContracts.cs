namespace Mastemis.Application.Problems.TestSets;

public sealed record ProblemTestMetadata(int TestIndex, string Group, string Visibility, string Checker,
    long InputLength, long? OutputLength, bool Published);
public sealed record ProblemTestSetVersion(Guid TestSetId, int Version, bool Published, string Source,
    Guid? GenerationOperationId, DateTimeOffset CreatedAtUtc, DateTimeOffset? PublishedAtUtc, int GroupCount, int TestCount, int HiddenTestCount);
public sealed record ProblemTestPage(IReadOnlyList<ProblemTestMetadata> Items, int Offset, int Limit, bool HasMore);

public sealed record ProblemTestContent(Stream Content, long Length, string Sha256);

public interface IProblemTestQueryService
{
    Task<IReadOnlyList<ProblemTestMetadata>> ListAsync(Guid problemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProblemTestSetVersion>> ListVersionsAsync(Guid problemId, CancellationToken cancellationToken);
    Task<ProblemTestPage> ListPageAsync(Guid problemId, Guid testSetId, int offset, int limit, CancellationToken cancellationToken);
    Task<ProblemTestContent> OpenInputAsync(Guid problemId, int testIndex, CancellationToken cancellationToken);
    Task<ProblemTestContent> OpenOutputAsync(Guid problemId, int testIndex, CancellationToken cancellationToken);
}
