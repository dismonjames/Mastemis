using Mastemis.Application.Invigilation.Queries;

namespace Mastemis.Server.Endpoints.Queries;

public static class InvigilationQueryEndpoints
{
    public static RouteGroupBuilder MapInvigilationQueryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/invigilation/exams/{examId:guid}", async (Guid examId, int? eventLimit,
            InvigilationQueryService service, CancellationToken ct) => Results.Ok(
                await service.GetExamAsync(examId, eventLimit ?? 100, ct)));
        group.MapGet("/invigilation/rooms/{roomId:guid}", async (Guid roomId, int? eventLimit,
            InvigilationQueryService service, CancellationToken ct) => Results.Ok(
                await service.GetRoomAsync(roomId, eventLimit ?? 100, ct)));
        return group;
    }
}
