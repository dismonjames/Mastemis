using Mastemis.Application.Problems.Packages;

namespace Mastemis.Server.Endpoints.ProblemStudio.Packages;

public static class ProblemPackageEndpoints
{
    public static RouteGroupBuilder MapProblemPackageEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/packages/validate", async (HttpRequest request, IProblemPackageService packages, CancellationToken ct) =>
            Results.Ok(await packages.ValidateAsync(request.Body, ct)));
        group.MapGet("/drafts/{problemId:guid}/packages/export", async (Guid problemId, IProblemPackageService packages, CancellationToken ct) =>
        { var package = await packages.ExportAsync(problemId, ct); return Results.Stream(package.Content, "application/vnd.mastemis.problem+zip", $"{problemId:N}.mas"); });
        return group;
    }
}
