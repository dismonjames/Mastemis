using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Examinations;

public sealed record ExaminationSummary(Guid Id, string Title, string State, DateTimeOffset? StartsAtUtc = null, DateTimeOffset? EndsAtUtc = null);

public interface IExaminationClient
{
    Task<ExaminationSummary> CreateAsync(string title, CancellationToken cancellationToken);
    Task TransitionAsync(Guid examId, string action, CancellationToken cancellationToken);
    Task<ExaminationSummary?> GetSummaryAsync(Guid examId, CancellationToken cancellationToken);
}

public sealed class ExaminationClient(IApiTransport transport) : IExaminationClient
{
    public Task<ExaminationSummary> CreateAsync(string title, CancellationToken cancellationToken)
        => Required(transport.SendAsync<object, ExaminationSummary>(HttpMethod.Post, "/api/exams", new { title, idempotencyKey = Guid.NewGuid().ToString("N") }, null, cancellationToken));

    public Task TransitionAsync(Guid examId, string action, CancellationToken cancellationToken)
        => transport.SendAsync(HttpMethod.Post, $"/api/exams/{examId:D}/{action}", new { }, Guid.NewGuid().ToString("N"), cancellationToken);

    public Task<ExaminationSummary?> GetSummaryAsync(Guid examId, CancellationToken cancellationToken)
        => transport.GetAsync<ExaminationSummary>($"/api/exams/{examId:D}/summary", cancellationToken);

    private static async Task<T> Required<T>(Task<T?> task) where T : class => await task.ConfigureAwait(false) ?? throw new InvalidDataException("The server returned an empty response.");
}
