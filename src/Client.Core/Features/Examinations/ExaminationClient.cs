using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Examinations;

public sealed record ExaminationSummary(Guid Id, string Title, string State, DateTimeOffset? StartsAtUtc = null, DateTimeOffset? EndsAtUtc = null);
public sealed record ExaminationListItem(Guid Id, string Title, string Status, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartsAtUtc, DateTimeOffset? EndsAtUtc, int RoomCount, int CandidateCount, int ActiveSessionCount, int WarningCount);
public sealed record ExaminationDetails(ExaminationListItem Examination, IReadOnlyList<object> Rooms,
    IReadOnlyList<object> Candidates, IReadOnlyList<object> Sessions, IReadOnlyList<object> Problems, IReadOnlyList<object> Timeline);

public interface IExaminationClient
{
    Task<ExaminationSummary> CreateAsync(string title, CancellationToken cancellationToken);
    Task TransitionAsync(Guid examId, string action, CancellationToken cancellationToken);
    Task ScheduleAsync(Guid examId, DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc, CancellationToken cancellationToken);
    Task<ExaminationSummary?> GetSummaryAsync(Guid examId, CancellationToken cancellationToken);
    Task<PagedResponse<ExaminationListItem>?> ListAsync(string? search, string? status, int offset, CancellationToken cancellationToken);
    Task<ExaminationDetails?> GetDetailsAsync(Guid examId, CancellationToken cancellationToken);
}

public sealed class ExaminationClient(IApiTransport transport) : IExaminationClient
{
    public Task<ExaminationSummary> CreateAsync(string title, CancellationToken cancellationToken)
        => Required(transport.SendAsync<object, ExaminationSummary>(HttpMethod.Post, "/api/exams", new { title, idempotencyKey = Guid.NewGuid().ToString("N") }, null, cancellationToken));

    public Task TransitionAsync(Guid examId, string action, CancellationToken cancellationToken)
        => transport.SendAsync(HttpMethod.Post, $"/api/exams/{examId:D}/{action}", new { }, Guid.NewGuid().ToString("N"), cancellationToken);

    public Task ScheduleAsync(Guid examId, DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc, CancellationToken cancellationToken)
        => transport.SendAsync(HttpMethod.Post, $"/api/exams/{examId:D}/schedule",
            new { startsAtUtc, endsAtUtc, idempotencyKey = Guid.NewGuid().ToString("N") }, null, cancellationToken);

    public Task<ExaminationSummary?> GetSummaryAsync(Guid examId, CancellationToken cancellationToken)
        => transport.GetAsync<ExaminationSummary>($"/api/exams/{examId:D}/summary", cancellationToken);

    public Task<PagedResponse<ExaminationListItem>?> ListAsync(string? search, string? status, int offset, CancellationToken cancellationToken)
        => transport.GetAsync<PagedResponse<ExaminationListItem>>($"/api/queries/exams?search={Uri.EscapeDataString(search ?? "")}&status={Uri.EscapeDataString(status ?? "")}&offset={offset}&limit=50", cancellationToken);

    public Task<ExaminationDetails?> GetDetailsAsync(Guid examId, CancellationToken cancellationToken)
        => transport.GetAsync<ExaminationDetails>($"/api/queries/exams/{examId:D}", cancellationToken);

    private static async Task<T> Required<T>(Task<T?> task) where T : class => await task.ConfigureAwait(false) ?? throw new InvalidDataException("The server returned an empty response.");
}
