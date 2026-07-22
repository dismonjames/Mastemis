using Mastemis.Domain;

namespace Mastemis.Application.Problems.Authoring;

public sealed record DraftProblem(ProblemId Id, string Title, string DefaultLocale, IReadOnlyDictionary<string, string> Statements,
    long TimeLimitMilliseconds, long MemoryLimitBytes, long OutputLimitBytes, string Checker, string MasSource, string MasSha256);
public enum GenerationOperationStatus
{
    Pending, Validating, GeneratingInputs, WaitingForReferenceOutputs, Publishing,
    Completed, Failed, CancelRequested, Cancelled
}
public sealed record ProblemGenerationOperation(Guid Id, ProblemId ProblemId, GenerationOperationStatus Status, ulong Seed,
    string RuntimeVersion, DateTimeOffset CreatedAtUtc, DateTimeOffset? CompletedAtUtc, string? FailureCode,
    int ProgressNumerator = 0, int ProgressDenominator = 0, int GeneratedInputCount = 0,
    int ExpectedOutputCount = 0, Guid? PublishedTestSetId = null);
public sealed record GeneratedProblemTest(int Index, string Group, string InputObjectId, string InputSha256, long InputLength,
    string? OutputObjectId, string? OutputSha256, long? OutputLength, string Checker, string Visibility);
