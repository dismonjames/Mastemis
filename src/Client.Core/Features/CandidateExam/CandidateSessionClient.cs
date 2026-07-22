using System.Text;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.CandidateExam;

public sealed record CandidateSession(Guid Id, Guid ExamId, Guid RoomId, Guid CandidateId, string State, int WarningCount, Guid? FrozenRevisionId);
public sealed record DraftRevision(Guid Id, string Sha256, DateTimeOffset CreatedAtUtc);
public sealed record SubmissionItem(Guid Id, Guid SessionId, Guid ProblemId, Guid RevisionId, string Language, string State, bool IsFinal);

public interface ICandidateSessionClient
{
    Task<CandidateSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<DraftRevision> SaveDraftAsync(Guid sessionId, string source, CancellationToken cancellationToken);
    Task<IReadOnlyList<SubmissionItem>> ListSubmissionsAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<SubmissionItem> SubmitAsync(Guid sessionId, Guid problemId, Guid revisionId, string language, CancellationToken cancellationToken);
}

public sealed class CandidateSessionClient(IApiTransport transport) : ICandidateSessionClient
{
    public Task<CandidateSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken) => transport.GetAsync<CandidateSession>($"/api/sessions/{sessionId:D}", cancellationToken);
    public async Task<DraftRevision> SaveDraftAsync(Guid sessionId, string source, CancellationToken cancellationToken)
        => await transport.SendAsync<object, DraftRevision>(HttpMethod.Post, $"/api/sessions/{sessionId:D}/drafts", new { contentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(source)), idempotencyKey = Guid.NewGuid().ToString("N") }, null, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The server returned no draft revision.");
    public async Task<IReadOnlyList<SubmissionItem>> ListSubmissionsAsync(Guid sessionId, CancellationToken cancellationToken)
        => await transport.GetAsync<List<SubmissionItem>>($"/api/sessions/{sessionId:D}/submissions", cancellationToken).ConfigureAwait(false) ?? [];
    public async Task<SubmissionItem> SubmitAsync(Guid sessionId, Guid problemId, Guid revisionId, string language, CancellationToken cancellationToken)
        => await transport.SendAsync<object, SubmissionItem>(HttpMethod.Post, $"/api/sessions/{sessionId:D}/submissions", new { problemId, revisionId, language, idempotencyKey = Guid.NewGuid().ToString("N") }, null, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The server returned no submission.");
}
