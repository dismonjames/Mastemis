using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio.Assets;

public sealed record ProblemAssetItem(Guid Id, Guid ProblemId, string LogicalName, string ContentType,
    string Sha256, long Length, Guid CreatedBy, DateTimeOffset CreatedAtUtc);

public interface IProblemAssetClient
{
    Task<IReadOnlyList<ProblemAssetItem>> ListAsync(Guid problemId, CancellationToken cancellationToken);
    Task<ProblemAssetItem> UploadAsync(Guid problemId, string name, string contentType, Stream content, CancellationToken cancellationToken);
    Task<Stream> DownloadAsync(Guid problemId, Guid assetId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid problemId, Guid assetId, CancellationToken cancellationToken);
}

public sealed class ProblemAssetClient(IApiTransport transport) : IProblemAssetClient
{
    public async Task<IReadOnlyList<ProblemAssetItem>> ListAsync(Guid id, CancellationToken ct) =>
        await transport.GetAsync<List<ProblemAssetItem>>($"/api/problem-studio/drafts/{id:D}/assets", ct).ConfigureAwait(false) ?? [];
    public async Task<ProblemAssetItem> UploadAsync(Guid id, string name, string type, Stream content, CancellationToken ct) =>
        await transport.UploadAsync<ProblemAssetItem>(HttpMethod.Post,
            $"/api/problem-studio/drafts/{id:D}/assets?logicalName={Uri.EscapeDataString(name)}&contentType={Uri.EscapeDataString(type)}",
            content, type, null, ct).ConfigureAwait(false) ?? throw new InvalidDataException("The server returned no asset metadata.");
    public Task<Stream> DownloadAsync(Guid id, Guid assetId, CancellationToken ct) =>
        transport.DownloadAsync($"/api/problem-studio/drafts/{id:D}/assets/{assetId:D}", ct);
    public Task DeleteAsync(Guid id, Guid assetId, CancellationToken ct) => transport.SendAsync(HttpMethod.Delete,
        $"/api/problem-studio/drafts/{id:D}/assets/{assetId:D}", new { }, null, ct);
}
