using Mastemis.Application.Problems.Generation;
using Mastemis.Domain;

namespace Mastemis.Server.Endpoints.ProblemStudio.Mas;

public static class ProblemMasEndpoints
{
    public static RouteGroupBuilder MapProblemMasEndpoints(this RouteGroupBuilder group)
    {
        group.MapPut("/drafts/{problemId:guid}/mas", async (Guid problemId, UpdateMasSourceRequest request,
            ProblemStudioService service, CancellationToken ct) =>
        { await service.SaveMasAsync(new(problemId), request.Source, ct); return Results.NoContent(); });
        group.MapPost("/drafts/{problemId:guid}/mas/validate", async (Guid problemId, ValidateMasSourceRequest request,
            ProblemStudioService service, CancellationToken ct) =>
            Results.Ok(await service.ValidateAsync(new(problemId), request.Source, ct)));
        group.MapPost("/drafts/{problemId:guid}/mas/preview", async (Guid problemId, PreviewMasRequest request,
            ProblemStudioService service, CancellationToken ct) =>
            Results.Ok(await service.PreviewAsync(new(problemId), request.Source, request.Seed, request.MaximumTests, ct)));
        return group;
    }
}
