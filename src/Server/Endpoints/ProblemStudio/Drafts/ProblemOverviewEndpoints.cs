using Mastemis.Application.Problems.Overview;

namespace Mastemis.Server.Endpoints.ProblemStudio.Drafts;

public static class ProblemOverviewEndpoints
{
    public static RouteGroupBuilder MapProblemOverviewEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/drafts/{problemId:guid}/overview", async (Guid problemId, ProblemOverviewService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(problemId, ct)));
        return group;
    }
}
