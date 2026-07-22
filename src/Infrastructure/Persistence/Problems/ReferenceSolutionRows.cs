namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class ReferenceSolutionRevisionRow
{
    public Guid Id { get; set; }
    public Guid ProblemId { get; set; }
    public string Language { get; set; } = string.Empty;
    public string CompileProfile { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public bool IsCurrent { get; set; }
    public bool Enabled { get; set; }
}

public sealed class ReferenceSolutionSourceRow
{
    public Guid RevisionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ObjectId { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long Length { get; set; }
}
