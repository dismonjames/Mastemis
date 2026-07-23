using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio.ReferenceSolution;

public sealed record ReferenceSourceItem(string FileName, string Sha256, long Length);
public sealed record ReferenceRevision(Guid RevisionId, Guid ProblemId, string Language,
    IReadOnlyList<ReferenceSourceItem> Sources, Guid CreatedBy, DateTimeOffset CreatedAtUtc, bool Enabled);
public sealed record ReferenceSourceUpdate(string FileName, string ContentBase64);

public interface IReferenceSolutionClient
{
    Task<ReferenceRevision?> GetAsync(Guid problemId, CancellationToken cancellationToken);
    Task<ReferenceRevision?> SaveAsync(Guid problemId, string language, IReadOnlyList<ReferenceSourceUpdate> sources, CancellationToken cancellationToken);
    Task<Stream> OpenSourceAsync(Guid problemId, Guid revisionId, string fileName, CancellationToken cancellationToken);
}

public sealed class ReferenceSolutionClient(IApiTransport transport) : IReferenceSolutionClient
{
    public Task<ReferenceRevision?> GetAsync(Guid id, CancellationToken ct) =>
        transport.GetAsync<ReferenceRevision>($"/api/problem-studio/drafts/{id:D}/reference-solution", ct);
    public Task<ReferenceRevision?> SaveAsync(Guid id, string language, IReadOnlyList<ReferenceSourceUpdate> sources, CancellationToken ct) =>
        transport.SendAsync<object, ReferenceRevision>(HttpMethod.Put, $"/api/problem-studio/drafts/{id:D}/reference-solution",
            new { language, sources }, Guid.NewGuid().ToString("N"), ct);
    public Task<Stream> OpenSourceAsync(Guid id, Guid revisionId, string fileName, CancellationToken ct) =>
        transport.DownloadAsync($"/api/problem-studio/drafts/{id:D}/reference-solution/{revisionId:D}/sources/{Uri.EscapeDataString(fileName)}", ct);
}
