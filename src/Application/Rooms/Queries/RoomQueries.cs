using Mastemis.Application.Queries;

namespace Mastemis.Application.Rooms.Queries;

public sealed record RoomListQuery(string? Search, int Offset = 0, int Limit = 50);
public sealed record RoomInvigilatorItem(Guid UserId, string DisplayName, DateTimeOffset AssignedAtUtc);
public sealed record RoomListItem(Guid Id, Guid ExamId, string Code, string Name, int? Capacity,
    int CandidateCount, int ConnectedCount, int DisconnectedCount, int WarningCount,
    IReadOnlyList<RoomInvigilatorItem> Invigilators);

public interface IRoomQueryStore
{
    Task<PagedResult<RoomListItem>> ListAsync(Guid examId, RoomListQuery query, CancellationToken cancellationToken);
    Task<RoomListItem?> GetAsync(Guid roomId, CancellationToken cancellationToken);
}

public sealed class RoomQueryService(IRoomQueryStore store, IAuthorizationService authorization)
{
    public async Task<PagedResult<RoomListItem>> ListAsync(Guid examId, RoomListQuery query, CancellationToken cancellationToken)
    {
        Validate(query.Offset, query.Limit);
        await authorization.EnsureAsync("exam.realtime", examId, cancellationToken);
        return await store.ListAsync(examId, query, cancellationToken);
    }

    public async Task<RoomListItem> GetAsync(Guid roomId, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("room.realtime", roomId, cancellationToken);
        return await store.GetAsync(roomId, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Room not found.");
    }

    private static void Validate(int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > 100)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "The requested room page is invalid.");
    }
}
