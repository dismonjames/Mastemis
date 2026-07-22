using Mastemis.Application;
using Mastemis.Application.Problems.Authoring;
using Mastemis.Application.Problems.Generation;
using Mastemis.Domain;

namespace Mastemis.Server.Endpoints.ProblemStudio.Drafts;

public static class ProblemDraftEndpoints
{
    public static RouteGroupBuilder MapProblemDraftEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/drafts", async (CreateProblemDraftRequest request, ProblemStudioService service, CancellationToken ct) =>
        {
            var draft = await service.CreateAsync(request.Title, request.DefaultLocale, ct);
            return Results.Created($"/api/problem-studio/drafts/{draft.Id.Value}", Response(draft));
        });
        group.MapGet("/drafts/{problemId:guid}", async (Guid problemId, IProblemStudioStore store,
            Mastemis.Application.IAuthorizationService authorization, CancellationToken ct) =>
        {
            await authorization.EnsureAsync("problem.manage", problemId, ct);
            var draft = await store.GetAsync(new(problemId), ct) ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem not found.");
            return Results.Ok(Response(draft));
        });
        return group;
    }

    private static ProblemDraftResponse Response(DraftProblem draft) => new(draft.Id.Value, draft.Title, draft.DefaultLocale,
        draft.TimeLimitMilliseconds, draft.MemoryLimitBytes, draft.OutputLimitBytes, draft.Checker, draft.MasSha256);
}
