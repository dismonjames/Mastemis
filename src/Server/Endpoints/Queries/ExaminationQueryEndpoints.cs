using Mastemis.Application.Examinations.Queries;

namespace Mastemis.Server.Endpoints.Queries;

public static class ExaminationQueryEndpoints
{
    public static RouteGroupBuilder MapExaminationQueryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/exams", async (string? search, string? status, DateTimeOffset? fromUtc, DateTimeOffset? toUtc,
            int? offset, int? limit, ExaminationQueryService service, CancellationToken ct) => Results.Ok(
                await service.ListAsync(new(search, status, fromUtc, toUtc, offset ?? 0, limit ?? 50), ct)));
        group.MapGet("/exams/{examId:guid}", async (Guid examId, ExaminationQueryService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(examId, ct)));
        return group;
    }
}
