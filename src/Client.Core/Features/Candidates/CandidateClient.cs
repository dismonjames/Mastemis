using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Candidates;

public sealed record CandidateRegistration(Guid Id, Guid UserId, string RegistrationCode);

public interface ICandidateClient
{
    Task<CandidateRegistration> RegisterAsync(Guid examId, Guid userId, string code, CancellationToken cancellationToken);
}

public sealed class CandidateClient(IApiTransport transport) : ICandidateClient
{
    public async Task<CandidateRegistration> RegisterAsync(Guid examId, Guid userId, string code, CancellationToken cancellationToken)
        => await transport.SendAsync<object, CandidateRegistration>(HttpMethod.Post, $"/api/exams/{examId:D}/candidates",
            new { userId, registrationCode = code, idempotencyKey = Guid.NewGuid().ToString("N") }, null, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The server returned an empty candidate response.");
}
