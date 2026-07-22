using Mastemis.Application.Workers.Queries;

namespace Mastemis.Server.Endpoints.Queries;

public static class WorkerQueryEndpoints
{
    public static RouteGroupBuilder MapWorkerQueryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/workers", async (string? search, string? readiness, int? offset, int? limit,
            WorkerInventoryQueryService service, CancellationToken ct) => Results.Ok(await service.ListAsync(
                new(search, readiness, offset ?? 0, limit ?? 50), ct)));
        return group;
    }
}
