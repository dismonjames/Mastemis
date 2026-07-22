namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class ProblemDraftRow
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AuthorsJson { get; set; } = "[]";
    public string TagsJson { get; set; } = "[]";
    public string Difficulty { get; set; } = "unspecified";
    public string AcceptedLanguagesJson { get; set; } = "[\"cpp\",\"csharp\"]";
    public string DefaultLocale { get; set; } = string.Empty;
    public long TimeLimitMilliseconds { get; set; } = 1000;
    public long MemoryLimitBytes { get; set; } = 268_435_456;
    public long OutputLimitBytes { get; set; } = 1_048_576;
    public string Checker { get; set; } = "exact";
    public string MasSource { get; set; } = string.Empty;
    public string MasSha256 { get; set; } = string.Empty;
    public int MasRevision { get; set; }
    public string MasValidationJson { get; set; } = "[]";
    public DateTimeOffset? MasValidatedAtUtc { get; set; }
    public string MasRuntimeVersion { get; set; } = "mas-runtime/1.0;prng=splitmix64-v1";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public int Version { get; set; } = 1;
}
