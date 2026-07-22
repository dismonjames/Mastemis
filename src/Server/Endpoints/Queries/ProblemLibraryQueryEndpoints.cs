using Mastemis.Application.Problems.Queries;

namespace Mastemis.Server.Endpoints.Queries;

public static class ProblemLibraryQueryEndpoints
{
    public static RouteGroupBuilder MapProblemLibraryQueryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/problems", async (string? search, string? status, string? difficulty, int? page, int? pageSize,
            ProblemLibraryQueryService service, CancellationToken ct) => Results.Ok(await service.SearchAsync(
                new(search, status, difficulty, page ?? 1, pageSize ?? 50), ct)));
        return group;
    }
}
