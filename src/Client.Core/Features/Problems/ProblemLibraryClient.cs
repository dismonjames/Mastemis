using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Problems;

public sealed record ProblemLibraryItem(Guid ProblemId, string Title, string Status, string Difficulty,
    IReadOnlyList<string> Tags, IReadOnlyList<string> Authors, int? CurrentTestSetVersion,
    int ExaminationAssignmentCount, string Permission, DateTimeOffset UpdatedAtUtc);
public interface IProblemLibraryClient { Task<PagedResponse<ProblemLibraryItem>?> SearchAsync(string? search, CancellationToken cancellationToken); }
public sealed class ProblemLibraryClient(IApiTransport transport) : IProblemLibraryClient
{
    public Task<PagedResponse<ProblemLibraryItem>?> SearchAsync(string? search, CancellationToken cancellationToken) =>
        transport.GetAsync<PagedResponse<ProblemLibraryItem>>($"/api/queries/problems?search={Uri.EscapeDataString(search ?? "")}&page=1&pageSize=100", cancellationToken);
}
