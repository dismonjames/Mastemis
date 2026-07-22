using Mastemis.Application;
using Mastemis.Application.Problems.ReferenceOutputs;
using Mastemis.Domain;

namespace Mastemis.Server.Endpoints.ProblemStudio.ReferenceSolutions;

public static class ReferenceSolutionEndpoints
{
    public static RouteGroupBuilder MapReferenceSolutionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/drafts/{problemId:guid}/reference-solution", async (Guid problemId, ReferenceSolutionService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(new(problemId), ct)));
        group.MapPut("/drafts/{problemId:guid}/reference-solution", async (Guid problemId, UpdateReferenceSolutionRequest request,
            ReferenceSolutionService service, CancellationToken ct) =>
        {
            var sources = request.Sources.Select(x => new ReferenceSolutionSourceInput(x.FileName, Decode(x.ContentBase64))).ToArray();
            return Results.Ok(await service.SaveAsync(new(problemId), request.Language, sources, ct));
        });
        group.MapGet("/drafts/{problemId:guid}/reference-solution/{revisionId:guid}/sources/{fileName}", async (Guid problemId,
            Guid revisionId, string fileName, ReferenceSolutionService service, CancellationToken ct) =>
        { var source = await service.OpenSourceAsync(new(problemId), revisionId, fileName, ct); return Results.Stream(source.Content, "text/plain; charset=utf-8", source.FileName); });
        return group;
    }
    private static byte[] Decode(string value)
    { try { return Convert.FromBase64String(value); } catch (FormatException) { throw new ApplicationFailure(ErrorCodes.InvalidInput, "Reference source must be base64 encoded."); } }
}
