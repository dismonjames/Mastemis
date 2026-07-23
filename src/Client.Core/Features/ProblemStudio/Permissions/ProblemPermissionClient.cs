using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio.Permissions;

public sealed record ProblemPermissionItem(Guid ProblemId, Guid UserId, string Role, string Status,
    Guid AssignedBy, DateTimeOffset AssignedAtUtc, DateTimeOffset? ExpiresAtUtc);

public interface IProblemPermissionClient
{
    Task<IReadOnlyList<ProblemPermissionItem>> ListAsync(Guid problemId, CancellationToken cancellationToken);
    Task<ProblemPermissionItem?> AssignAsync(Guid problemId, Guid userId, string role, DateTimeOffset? expires, CancellationToken cancellationToken);
    Task RevokeAsync(Guid problemId, Guid userId, CancellationToken cancellationToken);
}

public sealed class ProblemPermissionClient(IApiTransport transport) : IProblemPermissionClient
{
    public async Task<IReadOnlyList<ProblemPermissionItem>> ListAsync(Guid id, CancellationToken ct) =>
        await transport.GetAsync<List<ProblemPermissionItem>>($"/api/problem-studio/drafts/{id:D}/authors", ct).ConfigureAwait(false) ?? [];
    public Task<ProblemPermissionItem?> AssignAsync(Guid id, Guid userId, string role, DateTimeOffset? expires, CancellationToken ct) =>
        transport.SendAsync<object, ProblemPermissionItem>(HttpMethod.Put, $"/api/problem-studio/drafts/{id:D}/authors/{userId:D}", new { role, expiresAtUtc = expires }, Guid.NewGuid().ToString("N"), ct);
    public Task RevokeAsync(Guid id, Guid userId, CancellationToken ct) => transport.SendAsync(HttpMethod.Delete,
        $"/api/problem-studio/drafts/{id:D}/authors/{userId:D}", new { }, null, ct);
}
