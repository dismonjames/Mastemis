namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class ProblemAssetRow
{
    public Guid Id { get; set; }
    public Guid ProblemId { get; set; }
    public string LogicalName { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string ObjectId { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long Length { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
