using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio.Permissions;

public sealed record ProblemPermissionItem(Guid ProblemId, Guid UserId, string Role, string Status,
    Guid AssignedBy, DateTimeOffset AssignedAtUtc, DateTimeOffset? ExpiresAtUtc);
public sealed record ProblemExamAssignmentItem(Guid ProblemId, Guid ExamId, string ExamTitle, string ExamState,
    Guid AssignedBy, DateTimeOffset AssignedAtUtc);

public interface IProblemPermissionClient
{
    Task<IReadOnlyList<ProblemPermissionItem>> ListAsync(Guid problemId, CancellationToken cancellationToken);
    Task<ProblemPermissionItem?> AssignAsync(Guid problemId, Guid userId, string role, DateTimeOffset? expires, CancellationToken cancellationToken);
    Task RevokeAsync(Guid problemId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProblemExamAssignmentItem>> ListExamsAsync(Guid problemId, CancellationToken cancellationToken);
    Task AssignExamAsync(Guid problemId, Guid examId, CancellationToken cancellationToken);
    Task RemoveExamAsync(Guid problemId, Guid examId, CancellationToken cancellationToken);
}

public sealed class ProblemPermissionClient(IApiTransport transport) : IProblemPermissionClient
{
    public async Task<IReadOnlyList<ProblemPermissionItem>> ListAsync(Guid id, CancellationToken ct) =>
        await transport.GetAsync<List<ProblemPermissionItem>>($"/api/problem-studio/drafts/{id:D}/authors", ct).ConfigureAwait(false) ?? [];
    public Task<ProblemPermissionItem?> AssignAsync(Guid id, Guid userId, string role, DateTimeOffset? expires, CancellationToken ct) =>
        transport.SendAsync<object, ProblemPermissionItem>(HttpMethod.Put, $"/api/problem-studio/drafts/{id:D}/authors/{userId:D}", new { role, expiresAtUtc = expires }, Guid.NewGuid().ToString("N"), ct);
    public Task RevokeAsync(Guid id, Guid userId, CancellationToken ct) => transport.SendAsync(HttpMethod.Delete,
        $"/api/problem-studio/drafts/{id:D}/authors/{userId:D}", new { }, null, ct);
    public async Task<IReadOnlyList<ProblemExamAssignmentItem>> ListExamsAsync(Guid id, CancellationToken ct) =>
        await transport.GetAsync<List<ProblemExamAssignmentItem>>($"/api/problem-studio/drafts/{id:D}/exams", ct).ConfigureAwait(false) ?? [];
    public Task AssignExamAsync(Guid id, Guid examId, CancellationToken ct) => transport.SendAsync(HttpMethod.Put,
        $"/api/problem-studio/drafts/{id:D}/exams/{examId:D}", new { }, Guid.NewGuid().ToString("N"), ct);
    public Task RemoveExamAsync(Guid id, Guid examId, CancellationToken ct) => transport.SendAsync(HttpMethod.Delete,
        $"/api/problem-studio/drafts/{id:D}/exams/{examId:D}", new { }, null, ct);
}
