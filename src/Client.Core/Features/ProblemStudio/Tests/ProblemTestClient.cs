using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio.Tests;

public sealed record ProblemTestItem(int TestIndex, string Group, string Visibility, string Checker,
    long InputLength, long? OutputLength, bool Published);
public sealed record ProblemTestSetItem(Guid TestSetId, int Version, bool Published, string Source,
    Guid? GenerationOperationId, DateTimeOffset CreatedAtUtc, DateTimeOffset? PublishedAtUtc, int GroupCount, int TestCount, int HiddenTestCount);
public sealed record ProblemTestPage(IReadOnlyList<ProblemTestItem> Items, int Offset, int Limit, bool HasMore);

public interface IProblemTestClient
{
    Task<IReadOnlyList<ProblemTestItem>> ListAsync(Guid problemId, CancellationToken cancellationToken);
    Task<Stream> OpenInputAsync(Guid problemId, int index, CancellationToken cancellationToken);
    Task<Stream> OpenOutputAsync(Guid problemId, int index, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProblemTestSetItem>> ListVersionsAsync(Guid problemId, CancellationToken cancellationToken);
    Task<ProblemTestPage?> ListPageAsync(Guid problemId, Guid testSetId, int offset, int limit, CancellationToken cancellationToken);
}

public sealed class ProblemTestClient(IApiTransport transport) : IProblemTestClient
{
    public async Task<IReadOnlyList<ProblemTestItem>> ListAsync(Guid id, CancellationToken ct) =>
        await transport.GetAsync<List<ProblemTestItem>>($"/api/problem-studio/drafts/{id:D}/tests", ct).ConfigureAwait(false) ?? [];
    public Task<Stream> OpenInputAsync(Guid id, int index, CancellationToken ct) => transport.DownloadAsync($"/api/problem-studio/drafts/{id:D}/tests/{index}/input", ct);
    public Task<Stream> OpenOutputAsync(Guid id, int index, CancellationToken ct) => transport.DownloadAsync($"/api/problem-studio/drafts/{id:D}/tests/{index}/output", ct);
    public async Task<IReadOnlyList<ProblemTestSetItem>> ListVersionsAsync(Guid id, CancellationToken ct) =>
        await transport.GetAsync<List<ProblemTestSetItem>>($"/api/problem-studio/drafts/{id:D}/test-sets", ct).ConfigureAwait(false) ?? [];
    public Task<ProblemTestPage?> ListPageAsync(Guid id, Guid setId, int offset, int limit, CancellationToken ct) =>
        transport.GetAsync<ProblemTestPage>($"/api/problem-studio/drafts/{id:D}/test-sets/{setId:D}/tests?offset={offset}&limit={limit}", ct);
}
