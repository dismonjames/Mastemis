using Mastemis.Application.Problems.Generation;

namespace Mastemis.Server.Endpoints.ProblemStudio.Generation;

public static class ProblemGenerationEndpoints
{
    public static RouteGroupBuilder MapProblemGenerationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/drafts/{problemId:guid}/generation", async (Guid problemId, StartGenerationRequest request,
            ProblemStudioService service, CancellationToken ct) =>
        {
            var operation = await service.GenerateAsync(new(problemId), request.Seed, ct);
            return Results.Accepted($"/api/problem-studio/drafts/{problemId}/generation/{operation.Id}", operation);
        });
        group.MapGet("/drafts/{problemId:guid}/generation/{operationId:guid}", async (Guid problemId, Guid operationId,
            ProblemStudioService service, CancellationToken ct) =>
        {
            var operation = await service.GetStatusAsync(operationId, new(problemId), ct);
            return operation is null ? Results.NotFound() : Results.Ok(operation);
        });
        group.MapDelete("/drafts/{problemId:guid}/generation/{operationId:guid}", async (Guid problemId, Guid operationId,
            ProblemStudioService service, CancellationToken ct) =>
        { await service.CancelAsync(operationId, new(problemId), ct); return Results.Accepted(); });
        return group;
    }
}
