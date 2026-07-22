using Mastemis.Application.Administration;
using Mastemis.Application.Queries;
using Mastemis.Domain;

namespace Mastemis.Application.Problems.Queries;

public sealed record ProblemLibraryRequest(string? Search, string? Status, string? Difficulty, int Page = 1, int PageSize = 50);
public sealed record ProblemLibraryItem(Guid ProblemId, string Title, string Status, string Difficulty,
    IReadOnlyList<string> Tags, IReadOnlyList<string> Authors, int? CurrentTestSetVersion,
    int ExaminationAssignmentCount, string Permission, DateTimeOffset UpdatedAtUtc);

public interface IProblemLibraryQueryStore
{
    Task<PagedResult<ProblemLibraryItem>> SearchAsync(UserId actorId, ProblemLibraryRequest request,
        CancellationToken cancellationToken);
}

public sealed class ProblemLibraryQueryService(IProblemLibraryQueryStore store, IAdministrationActor actor)
{
    public Task<PagedResult<ProblemLibraryItem>> SearchAsync(ProblemLibraryRequest request, CancellationToken cancellationToken)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 200)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Page must be positive and page size cannot exceed 200.");
        return store.SearchAsync(actor.UserId, request, cancellationToken);
    }
}
