using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Rooms;

public sealed record RoomSummary(Guid Id, Guid ExamId, string Name);
public sealed record RoomListItem(Guid Id, Guid ExamId, string Code, string Name, int? Capacity,
    int CandidateCount, int ConnectedCount, int DisconnectedCount, int WarningCount, IReadOnlyList<object> Invigilators);

public interface IRoomClient
{
    Task<RoomSummary> CreateAsync(Guid examId, string name, CancellationToken cancellationToken);
    Task<PagedResponse<RoomListItem>?> ListAsync(Guid examId, string? search, CancellationToken cancellationToken);
}

public sealed class RoomClient(IApiTransport transport) : IRoomClient
{
    public async Task<RoomSummary> CreateAsync(Guid examId, string name, CancellationToken cancellationToken)
        => await transport.SendAsync<object, RoomSummary>(HttpMethod.Post, $"/api/exams/{examId:D}/rooms",
            new { name, idempotencyKey = Guid.NewGuid().ToString("N") }, null, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The server returned an empty room response.");

    public Task<PagedResponse<RoomListItem>?> ListAsync(Guid examId, string? search, CancellationToken cancellationToken)
        => transport.GetAsync<PagedResponse<RoomListItem>>($"/api/queries/exams/{examId:D}/rooms?search={Uri.EscapeDataString(search ?? "")}&offset=0&limit=100", cancellationToken);
}
