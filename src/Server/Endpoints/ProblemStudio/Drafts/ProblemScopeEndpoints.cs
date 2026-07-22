using Mastemis.Application.Problems.Authorization;
using Mastemis.Domain;

namespace Mastemis.Server.Endpoints.ProblemStudio.Drafts;

public static class ProblemScopeEndpoints
{
    public static RouteGroupBuilder MapProblemScopeEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/drafts/{problemId:guid}/authors", async (Guid problemId, IProblemScopeAdministration scopes, CancellationToken ct) =>
            Results.Ok(await scopes.ListAuthorsAsync(new(problemId), ct)));
        group.MapPut("/drafts/{problemId:guid}/authors/{userId:guid}", async (Guid problemId, Guid userId,
            AssignProblemAuthorRequest request, IProblemScopeAdministration scopes, CancellationToken ct) =>
            Results.Ok(await scopes.AssignAuthorAsync(new(problemId), new(userId), request.Role, request.ExpiresAtUtc, ct)));
        group.MapDelete("/drafts/{problemId:guid}/authors/{userId:guid}", async (Guid problemId, Guid userId,
            IProblemScopeAdministration scopes, CancellationToken ct) =>
        { await scopes.RevokeAuthorAsync(new(problemId), new(userId), ct); return Results.NoContent(); });
        group.MapPut("/drafts/{problemId:guid}/exams/{examId:guid}", async (Guid problemId, Guid examId,
            IProblemScopeAdministration scopes, CancellationToken ct) =>
        { await scopes.AssignExamAsync(new(problemId), new(examId), ct); return Results.NoContent(); });
        group.MapDelete("/drafts/{problemId:guid}/exams/{examId:guid}", async (Guid problemId, Guid examId,
            IProblemScopeAdministration scopes, CancellationToken ct) =>
        { await scopes.RemoveExamAsync(new(problemId), new(examId), ct); return Results.NoContent(); });
        return group;
    }
}
