namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class ProblemStatementRow
{
    public Guid ProblemId { get; set; }
    public string Locale { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ObjectId { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long Length { get; set; }
    public int Revision { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
