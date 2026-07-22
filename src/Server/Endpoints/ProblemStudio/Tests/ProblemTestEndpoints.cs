using Mastemis.Application.Problems.TestSets;

namespace Mastemis.Server.Endpoints.ProblemStudio.Tests;

public static class ProblemTestEndpoints
{
    public static RouteGroupBuilder MapProblemTestEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/drafts/{problemId:guid}/tests", async (Guid problemId, IProblemTestQueryService tests, CancellationToken ct) =>
            Results.Ok(await tests.ListAsync(problemId, ct)));
        group.MapGet("/drafts/{problemId:guid}/tests/{testIndex:int}/input", async (Guid problemId, int testIndex,
            IProblemTestQueryService tests, CancellationToken ct) =>
        { var content = await tests.OpenInputAsync(problemId, testIndex, ct); return Results.Stream(content.Content, "application/octet-stream"); });
        group.MapGet("/drafts/{problemId:guid}/tests/{testIndex:int}/output", async (Guid problemId, int testIndex,
            IProblemTestQueryService tests, CancellationToken ct) =>
        { var content = await tests.OpenOutputAsync(problemId, testIndex, ct); return Results.Stream(content.Content, "application/octet-stream"); });
        return group;
    }
}
