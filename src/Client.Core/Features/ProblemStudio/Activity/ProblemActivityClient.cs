using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio.Activity;

public sealed record ProblemActivityItem(Guid EventId, Guid ProblemId, DateTimeOffset Timestamp,
    string Actor, string Kind, string Summary, string? CorrelationId, Guid? RelatedOperationId);
public sealed record ProblemActivityPage(IReadOnlyList<ProblemActivityItem> Items, int Offset, int Limit, bool HasMore);

public interface IProblemActivityClient
{
    Task<ProblemActivityPage?> ListAsync(Guid problemId, int offset, int limit, string? kind, CancellationToken cancellationToken);
}

public sealed class ProblemActivityClient(IApiTransport transport) : IProblemActivityClient
{
    public Task<ProblemActivityPage?> ListAsync(Guid id, int offset, int limit, string? kind, CancellationToken ct) =>
        transport.GetAsync<ProblemActivityPage>($"/api/problem-studio/drafts/{id:D}/activity?offset={offset}&limit={limit}&kind={Uri.EscapeDataString(kind ?? string.Empty)}", ct);
}
