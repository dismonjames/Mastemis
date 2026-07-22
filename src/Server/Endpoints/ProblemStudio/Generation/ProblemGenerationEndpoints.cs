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
        group.MapGet("/drafts/{problemId:guid}/generation/{operationId:guid}/progress",
            async (Guid problemId, Guid operationId, ProblemGenerationQueryService service, CancellationToken ct) =>
                Results.Ok(await service.GetProgressAsync(new(problemId), operationId, ct)));
        group.MapGet("/drafts/{problemId:guid}/generation/{operationId:guid}/diagnostics",
            async (Guid problemId, Guid operationId, int? offset, int? limit,
                ProblemGenerationQueryService service, CancellationToken ct) =>
                Results.Ok(await service.GetDiagnosticsAsync(
                    new(problemId), operationId, offset ?? 0, limit ?? 50, ct)));
        group.MapDelete("/drafts/{problemId:guid}/generation/{operationId:guid}", async (Guid problemId, Guid operationId,
            ProblemStudioService service, CancellationToken ct) =>
        { await service.CancelAsync(operationId, new(problemId), ct); return Results.Accepted(); });
        return group;
    }
}
