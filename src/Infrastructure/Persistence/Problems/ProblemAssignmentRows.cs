namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class ProblemAuthorAssignmentRow
{
    public Guid ProblemId { get; set; }
    public Guid UserId { get; set; }
    public int Role { get; set; }
    public int Status { get; set; }
    public Guid AssignedByUserId { get; set; }
    public DateTimeOffset AssignedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}

public sealed class ExamProblemAssignmentRow
{
    public Guid ExamId { get; set; }
    public Guid ProblemId { get; set; }
    public Guid AssignedByUserId { get; set; }
    public DateTimeOffset AssignedAtUtc { get; set; }
}
