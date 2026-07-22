namespace Mastemis.Application.Problems.TestSets;

public sealed record ProblemTestMetadata(int TestIndex, string Group, string Visibility, string Checker,
    long InputLength, long? OutputLength, bool Published);

public sealed record ProblemTestContent(Stream Content, long Length, string Sha256);

public interface IProblemTestQueryService
{
    Task<IReadOnlyList<ProblemTestMetadata>> ListAsync(Guid problemId, CancellationToken cancellationToken);
    Task<ProblemTestContent> OpenInputAsync(Guid problemId, int testIndex, CancellationToken cancellationToken);
    Task<ProblemTestContent> OpenOutputAsync(Guid problemId, int testIndex, CancellationToken cancellationToken);
}
