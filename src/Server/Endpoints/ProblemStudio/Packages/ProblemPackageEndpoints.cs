using Mastemis.Application.Problems.Packages;

namespace Mastemis.Server.Endpoints.ProblemStudio.Packages;

public static class ProblemPackageEndpoints
{
    public static RouteGroupBuilder MapProblemPackageEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/packages/validate", async (HttpRequest request, IProblemPackageService packages, CancellationToken ct) =>
            Results.Ok(await packages.ValidateAsync(request.Body, ct)));
        group.MapPost("/packages/import", async (HttpRequest request, IProblemPackageService packages, CancellationToken ct) =>
        {
            if (!request.Headers.TryGetValue("Idempotency-Key", out var key)) return Results.BadRequest();
            return Results.Created("/api/problem-studio/drafts", await packages.CreateNewAsync(request.Body, key.ToString(), ct));
        });
        group.MapPut("/drafts/{problemId:guid}/packages/import", async (Guid problemId, int expectedVersion,
            HttpRequest request, IProblemPackageService packages, CancellationToken ct) =>
        {
            if (!request.Headers.TryGetValue("Idempotency-Key", out var key)) return Results.BadRequest();
            return Results.Ok(await packages.ReplaceDraftAsync(problemId, expectedVersion, request.Body, key.ToString(), ct));
        });
        group.MapPost("/drafts/{problemId:guid}/packages/export", async (Guid problemId, HttpRequest request,
            IProblemPackageService packages, CancellationToken ct) =>
        {
            if (!request.Headers.TryGetValue("Idempotency-Key", out var key)) return Results.BadRequest();
            var package = await packages.ExportAsync(problemId, key.ToString(), ct);
            return Results.Ok(new { package.ExportId, package.Sha256, package.Length, package.CreatedAtUtc, package.ExpiresAtUtc });
        });
        group.MapGet("/drafts/{problemId:guid}/packages/exports", async (Guid problemId, IProblemPackageService packages, CancellationToken ct) =>
            Results.Ok(await packages.ListExportsAsync(problemId, ct)));
        group.MapGet("/drafts/{problemId:guid}/packages/exports/{exportId:guid}", async (Guid problemId, Guid exportId,
            IProblemPackageService packages, CancellationToken ct) =>
        { var package = await packages.OpenExportAsync(problemId, exportId, ct); return Results.Stream(package.Content, "application/vnd.mastemis.problem+zip", $"{problemId:N}.mas"); });
        group.MapDelete("/drafts/{problemId:guid}/packages/exports/{exportId:guid}", async (Guid problemId, Guid exportId,
            IProblemPackageService packages, CancellationToken ct) =>
        { await packages.ExpireExportAsync(problemId, exportId, ct); return Results.NoContent(); });
        return group;
    }
}
