namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class ProblemPackageImportRow
{
    public Guid Id { get; set; }
    public Guid ProblemId { get; set; }
    public string PackageSha256 { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class ProblemPackageExportRow
{
    public Guid Id { get; set; }
    public Guid ProblemId { get; set; }
    public string PackageSha256 { get; set; } = string.Empty;
    public string ObjectId { get; set; } = string.Empty;
    public long Length { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
