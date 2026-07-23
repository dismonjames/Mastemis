using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio.Tests;

public sealed record ProblemTestItem(int TestIndex, string Group, string Visibility, string Checker,
    long InputLength, long? OutputLength, bool Published);

public interface IProblemTestClient
{
    Task<IReadOnlyList<ProblemTestItem>> ListAsync(Guid problemId, CancellationToken cancellationToken);
    Task<Stream> OpenInputAsync(Guid problemId, int index, CancellationToken cancellationToken);
    Task<Stream> OpenOutputAsync(Guid problemId, int index, CancellationToken cancellationToken);
}

public sealed class ProblemTestClient(IApiTransport transport) : IProblemTestClient
{
    public async Task<IReadOnlyList<ProblemTestItem>> ListAsync(Guid id, CancellationToken ct) =>
        await transport.GetAsync<List<ProblemTestItem>>($"/api/problem-studio/drafts/{id:D}/tests", ct).ConfigureAwait(false) ?? [];
    public Task<Stream> OpenInputAsync(Guid id, int index, CancellationToken ct) => transport.DownloadAsync($"/api/problem-studio/drafts/{id:D}/tests/{index}/input", ct);
    public Task<Stream> OpenOutputAsync(Guid id, int index, CancellationToken ct) => transport.DownloadAsync($"/api/problem-studio/drafts/{id:D}/tests/{index}/output", ct);
}
