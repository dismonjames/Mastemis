using Mastemis.Application.Candidates.Queries;
using Mastemis.Application.Rooms.Queries;

namespace Mastemis.Server.Endpoints.Queries;

public static class RoomCandidateQueryEndpoints
{
    public static RouteGroupBuilder MapRoomCandidateQueryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/exams/{examId:guid}/rooms", async (Guid examId, string? search, int? offset, int? limit,
            RoomQueryService service, CancellationToken ct) => Results.Ok(await service.ListAsync(examId,
                new(search, offset ?? 0, limit ?? 50), ct)));
        group.MapGet("/rooms/{roomId:guid}", async (Guid roomId, RoomQueryService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(roomId, ct)));
        group.MapGet("/exams/{examId:guid}/candidates", async (Guid examId, string? search, string? accessState,
            string? sessionState, Guid? roomId, int? offset, int? limit, CandidateQueryService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(examId, new(search, accessState, sessionState, roomId,
                offset ?? 0, limit ?? 50), ct)));
        group.MapGet("/exams/{examId:guid}/candidates/{candidateId:guid}", async (Guid examId, Guid candidateId,
            CandidateQueryService service, CancellationToken ct) => Results.Ok(await service.GetAsync(examId, candidateId, ct)));
        return group;
    }
}
