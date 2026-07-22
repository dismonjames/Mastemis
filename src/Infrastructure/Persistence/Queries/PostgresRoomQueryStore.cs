using Mastemis.Application.Queries;
using Mastemis.Application.Rooms.Queries;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Queries;

public sealed class PostgresRoomQueryStore(MastemisDbContext db) : IRoomQueryStore
{
    public async Task<PagedResult<RoomListItem>> ListAsync(Guid examId, RoomListQuery request, CancellationToken cancellationToken)
    {
        var query = db.Rooms.AsNoTracking().Where(x => x.ExamId == examId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Name, $"%{term}%") || EF.Functions.ILike(x.Code, $"%{term}%"));
        }
        var total = await query.CountAsync(cancellationToken);
        var ids = await query.OrderBy(x => x.Name).Skip(request.Offset).Take(request.Limit).Select(x => x.Id).ToArrayAsync(cancellationToken);
        var items = new List<RoomListItem>(ids.Length);
        foreach (var id in ids)
        {
            var item = await GetAsync(id, cancellationToken);
            if (item is not null) items.Add(item);
        }
        return new(items, request.Offset, request.Limit, total);
    }

    public async Task<RoomListItem?> GetAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var room = await db.Rooms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == roomId, cancellationToken);
        if (room is null) return null;
        var invigilators = await (from assignment in db.RoomAssignments.AsNoTracking()
                                  join user in db.Users.AsNoTracking() on assignment.UserId equals user.Id
                                  where assignment.RoomId == roomId orderby user.DisplayName
                                  select new RoomInvigilatorItem(user.Id, user.DisplayName, assignment.AssignedAtUtc))
            .ToArrayAsync(cancellationToken);
        return new(room.Id, room.ExamId, room.Code, room.Name, null,
            await db.ExamSessions.Where(x => x.RoomId == roomId).Select(x => x.CandidateId).Distinct().CountAsync(cancellationToken),
            await db.ExamSessions.CountAsync(x => x.RoomId == roomId && x.State == (int)SessionState.Active, cancellationToken),
            await db.ExamSessions.CountAsync(x => x.RoomId == roomId && x.State == (int)SessionState.Disconnected, cancellationToken),
            await db.ConfirmedWarnings.CountAsync(x => x.RoomId == roomId, cancellationToken), invigilators);
    }
}
