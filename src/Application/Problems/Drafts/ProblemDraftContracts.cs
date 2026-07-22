using Mastemis.Domain;

namespace Mastemis.Application.Problems.Drafts;

public sealed record ProblemDraftDetails(ProblemId Id, string Title, IReadOnlyList<string> Authors,
    IReadOnlyList<string> Tags, string Difficulty, string DefaultLocale, IReadOnlyList<string> AcceptedLanguages,
    long TimeLimitMilliseconds, long MemoryLimitBytes, long OutputLimitBytes, string Checker, int Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProblemDraftUpdate(string Title, IReadOnlyList<string> Authors, IReadOnlyList<string> Tags,
    string Difficulty, string DefaultLocale, IReadOnlyList<string> AcceptedLanguages, long TimeLimitMilliseconds,
    long MemoryLimitBytes, long OutputLimitBytes, string Checker, int ExpectedVersion);

public interface IProblemDraftService
{
    Task<IReadOnlyList<ProblemDraftDetails>> ListAsync(CancellationToken cancellationToken);
    Task<ProblemDraftDetails> GetAsync(ProblemId problemId, CancellationToken cancellationToken);
    Task<ProblemDraftDetails> UpdateAsync(ProblemId problemId, ProblemDraftUpdate update, CancellationToken cancellationToken);
    Task DeleteAsync(ProblemId problemId, int expectedVersion, CancellationToken cancellationToken);
}
