using Mastemis.Application.Problems.Statements;
using Mastemis.Domain;

namespace Mastemis.Server.Endpoints.ProblemStudio.Statements;

public static class ProblemStatementEndpoints
{
    public static RouteGroupBuilder MapProblemStatementEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/drafts/{problemId:guid}/statements", async (Guid problemId, ProblemStatementService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(new(problemId), ct)));
        group.MapGet("/drafts/{problemId:guid}/statements/{locale}", async (Guid problemId, string locale, ProblemStatementService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(new(problemId), locale, ct)));
        group.MapPut("/drafts/{problemId:guid}/statements/{locale}", async (Guid problemId, string locale,
            UpdateProblemStatementRequest request, ProblemStatementService service, CancellationToken ct) =>
            Results.Ok(await service.SaveAsync(new(problemId), locale, request.Content, request.ExpectedRevision, ct)));
        group.MapDelete("/drafts/{problemId:guid}/statements/{locale}", async (Guid problemId, string locale,
            ProblemStatementService service, CancellationToken ct) =>
        { await service.DeleteAsync(new(problemId), locale, ct); return Results.NoContent(); });
        return group;
    }
}
