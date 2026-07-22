using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Rooms;

public sealed record RoomSummary(Guid Id, Guid ExamId, string Name);

public interface IRoomClient
{
    Task<RoomSummary> CreateAsync(Guid examId, string name, CancellationToken cancellationToken);
}

public sealed class RoomClient(IApiTransport transport) : IRoomClient
{
    public async Task<RoomSummary> CreateAsync(Guid examId, string name, CancellationToken cancellationToken)
        => await transport.SendAsync<object, RoomSummary>(HttpMethod.Post, $"/api/exams/{examId:D}/rooms",
            new { name, idempotencyKey = Guid.NewGuid().ToString("N") }, null, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The server returned an empty room response.");
}
