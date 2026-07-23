using Mastemis.Application.Problems.Activity;

namespace Mastemis.Server.Endpoints.ProblemStudio.Activity;

public static class ProblemActivityEndpoints
{
    public static RouteGroupBuilder MapProblemActivityEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/drafts/{problemId:guid}/activity", async (Guid problemId, int offset, int limit, string? kind,
            ProblemActivityService service, CancellationToken ct) => Results.Ok(await service.ListAsync(problemId,
                new(offset, limit == 0 ? 50 : limit, kind), ct)));
        return group;
    }
}
