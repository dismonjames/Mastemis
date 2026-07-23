using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Candidates;

public sealed record CandidateRegistration(Guid Id, Guid UserId, string RegistrationCode);
public sealed record CandidateListItem(Guid CandidateId, Guid UserId, string Username, string DisplayName,
    bool AccountEnabled, string RegistrationCode, string AccessState, Guid? SessionId, string? SessionState,
    Guid? RoomId, int WarningCount, DateTimeOffset? LatestActivityUtc);

public interface ICandidateClient
{
    Task<CandidateRegistration> RegisterAsync(Guid examId, Guid userId, string code, CancellationToken cancellationToken);
    Task<PagedResponse<CandidateListItem>?> ListAsync(Guid examId, string? search, CancellationToken cancellationToken);
    Task SetEnabledAsync(Guid userId, bool enabled, CancellationToken cancellationToken);
}

public sealed class CandidateClient(IApiTransport transport) : ICandidateClient
{
    public async Task<CandidateRegistration> RegisterAsync(Guid examId, Guid userId, string code, CancellationToken cancellationToken)
        => await transport.SendAsync<object, CandidateRegistration>(HttpMethod.Post, $"/api/exams/{examId:D}/candidates",
            new { userId, registrationCode = code, idempotencyKey = Guid.NewGuid().ToString("N") }, null, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The server returned an empty candidate response.");

    public Task<PagedResponse<CandidateListItem>?> ListAsync(Guid examId, string? search, CancellationToken cancellationToken)
        => transport.GetAsync<PagedResponse<CandidateListItem>>($"/api/queries/exams/{examId:D}/candidates?search={Uri.EscapeDataString(search ?? "")}&offset=0&limit=100", cancellationToken);
    public Task SetEnabledAsync(Guid userId, bool enabled, CancellationToken ct) => transport.SendAsync(HttpMethod.Post,
        $"/api/admin/users/{userId:D}/{(enabled ? "enable" : "disable")}", new { }, Guid.NewGuid().ToString("N"), ct);
}
