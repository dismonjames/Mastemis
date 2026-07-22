using Mastemis.Application;
using Mastemis.Application.Problems.Assets;
using Mastemis.Application.Problems.TestSets;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class PostgresProblemTestQueryService(MastemisDbContext db, IProblemObjectStorage objects,
    IAuthorizationService authorization) : IProblemTestQueryService
{
    public async Task<IReadOnlyList<ProblemTestMetadata>> ListAsync(Guid problemId, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("problem.read", problemId, cancellationToken);
        return await Query(problemId).OrderBy(x => x.Test.TestIndex).Select(x => new ProblemTestMetadata(x.Test.TestIndex,
            x.Test.Group, x.Test.Visibility, x.Test.Checker, x.Test.InputLength, x.Test.OutputLength, x.Set.Published))
            .ToArrayAsync(cancellationToken);
    }

    public Task<ProblemTestContent> OpenInputAsync(Guid problemId, int testIndex, CancellationToken cancellationToken) =>
        OpenAsync(problemId, testIndex, output: false, cancellationToken);

    public Task<ProblemTestContent> OpenOutputAsync(Guid problemId, int testIndex, CancellationToken cancellationToken) =>
        OpenAsync(problemId, testIndex, output: true, cancellationToken);

    private async Task<ProblemTestContent> OpenAsync(Guid problemId, int testIndex, bool output, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("problem.hidden", problemId, cancellationToken);
        var row = await Query(problemId).Where(x => x.Test.TestIndex == testIndex && x.Set.Published)
            .Select(x => new { x.Test.InputObjectId, x.Test.InputSha256, x.Test.InputLength, x.Test.OutputObjectId, x.Test.OutputSha256, x.Test.OutputLength })
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Published test was not found.");
        var objectId = output ? row.OutputObjectId : row.InputObjectId;
        var sha256 = output ? row.OutputSha256 : row.InputSha256;
        var length = output ? row.OutputLength : row.InputLength;
        if (objectId is null || sha256 is null || length is null) throw new ApplicationFailure(ErrorCodes.NotFound, "Test content was not found.");
        return new(await objects.OpenReadAsync(objectId, length.Value, cancellationToken), length.Value, sha256);
    }

    private IQueryable<TestProjection> Query(Guid problemId) =>
        from test in db.GeneratedTests.AsNoTracking()
        join set in db.GeneratedTestSets.AsNoTracking() on test.TestSetId equals set.Id
        where set.ProblemId == problemId
        select new TestProjection(test, set);

    private sealed record TestProjection(GeneratedTestRow Test, GeneratedTestSetRow Set);
}
