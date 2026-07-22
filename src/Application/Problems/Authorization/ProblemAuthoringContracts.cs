using Mastemis.Domain;

namespace Mastemis.Application.Problems.Authorization;

public enum ProblemAuthorRole { Owner, Editor, Reviewer, Viewer }
public enum ProblemAssignmentStatus { Active, Revoked }

public sealed record ProblemAuthorAssignment(ProblemId ProblemId, UserId UserId, ProblemAuthorRole Role,
    ProblemAssignmentStatus Status, UserId AssignedBy, DateTimeOffset AssignedAtUtc, DateTimeOffset? ExpiresAtUtc);

public interface IProblemScopeAdministration
{
    Task<ProblemAuthorAssignment> AssignAuthorAsync(ProblemId problemId, UserId userId, ProblemAuthorRole role,
        DateTimeOffset? expiresAtUtc, CancellationToken cancellationToken);
    Task RevokeAuthorAsync(ProblemId problemId, UserId userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProblemAuthorAssignment>> ListAuthorsAsync(ProblemId problemId, CancellationToken cancellationToken);
    Task AssignExamAsync(ProblemId problemId, ExamId examId, CancellationToken cancellationToken);
    Task RemoveExamAsync(ProblemId problemId, ExamId examId, CancellationToken cancellationToken);
}
