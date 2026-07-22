using Mastemis.Application;
using Mastemis.Application.Problems.Assets;
using Mastemis.Domain;

namespace Mastemis.Server.Endpoints.ProblemStudio.Assets;

public static class ProblemAssetEndpoints
{
    public static RouteGroupBuilder MapProblemAssetEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/drafts/{problemId:guid}/assets", async (Guid problemId, HttpRequest request,
            ProblemAssetService service, CancellationToken ct) =>
        {
            if (!request.Query.TryGetValue("logicalName", out var name) || !request.Query.TryGetValue("contentType", out var type))
                throw new ApplicationFailure(ErrorCodes.InvalidInput, "Asset name and content type are required.");
            return Results.Ok(await service.UploadAsync(new(problemId), name.ToString(), type.ToString(), request.Body, ct));
        });
        group.MapGet("/drafts/{problemId:guid}/assets", async (Guid problemId, ProblemAssetService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(new(problemId), ct)));
        group.MapGet("/drafts/{problemId:guid}/assets/{assetId:guid}", async (Guid problemId, Guid assetId,
            ProblemAssetService service, CancellationToken ct) =>
        { var asset = await service.OpenAsync(new(problemId), assetId, ct); return Results.Stream(asset.Content, asset.Metadata.ContentType, asset.Metadata.LogicalName); });
        group.MapDelete("/drafts/{problemId:guid}/assets/{assetId:guid}", async (Guid problemId, Guid assetId,
            ProblemAssetService service, CancellationToken ct) =>
        { await service.DeleteAsync(new(problemId), assetId, ct); return Results.NoContent(); });
        return group;
    }
}
