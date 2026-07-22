namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class ProblemDraftRow
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DefaultLocale { get; set; } = string.Empty;
    public long TimeLimitMilliseconds { get; set; } = 1000;
    public long MemoryLimitBytes { get; set; } = 268_435_456;
    public long OutputLimitBytes { get; set; } = 1_048_576;
    public string Checker { get; set; } = "exact";
    public string MasSource { get; set; } = string.Empty;
    public string MasSha256 { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public int Version { get; set; } = 1;
}
