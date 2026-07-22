namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class ProblemGenerationOperationRow
{
    public Guid Id { get; set; }
    public Guid ProblemId { get; set; }
    public int DraftVersion { get; set; }
    public Guid ActorUserId { get; set; }
    public int Status { get; set; }
    public ulong Seed { get; set; }
    public string RuntimeVersion { get; set; } = string.Empty;
    public string PrngAlgorithm { get; set; } = "splitmix64-v1";
    public string MasSourceSha256 { get; set; } = string.Empty;
    public int RequestedTestCount { get; set; }
    public int GeneratedInputCount { get; set; }
    public int ExpectedOutputCount { get; set; }
    public int ProgressNumerator { get; set; }
    public int ProgressDenominator { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? CancellationRequestedAtUtc { get; set; }
    public string? DiagnosticSummary { get; set; }
    public string? FailureCode { get; set; }
    public Guid? PublishedTestSetId { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

public sealed class GeneratedTestSetRow
{
    public Guid Id { get; set; }
    public Guid ProblemId { get; set; }
    public Guid GenerationOperationId { get; set; }
    public int Version { get; set; }
    public bool Published { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
}

public sealed class GeneratedTestRow
{
    public Guid Id { get; set; }
    public Guid TestSetId { get; set; }
    public int TestIndex { get; set; }
    public string Group { get; set; } = string.Empty;
    public string Visibility { get; set; } = "hidden";
    public string Checker { get; set; } = "exact";
    public string InputObjectId { get; set; } = string.Empty;
    public string InputSha256 { get; set; } = string.Empty;
    public long InputLength { get; set; }
    public string? OutputObjectId { get; set; }
    public string? OutputSha256 { get; set; }
    public long? OutputLength { get; set; }
}
