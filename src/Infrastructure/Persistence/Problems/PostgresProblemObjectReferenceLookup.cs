using Mastemis.Application.Problems.Assets;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class PostgresProblemObjectReferenceLookup(MastemisDbContext db) : IProblemObjectReferenceLookup
{
    public async Task<IReadOnlySet<string>> FindReferencedAsync(IReadOnlyCollection<string> objectIds,
        CancellationToken cancellationToken)
    {
        if (objectIds.Count == 0) return new HashSet<string>(StringComparer.Ordinal);
        var testInputs = await db.GeneratedTests.AsNoTracking().Join(db.GeneratedTestSets.Where(x => x.Published), x => x.TestSetId, x => x.Id, (test, _) => test)
            .Where(x => objectIds.Contains(x.InputObjectId))
            .Select(x => x.InputObjectId).ToListAsync(cancellationToken);
        var testOutputs = await db.GeneratedTests.AsNoTracking().Where(x => x.OutputObjectId != null && objectIds.Contains(x.OutputObjectId))
            .Select(x => x.OutputObjectId!).ToListAsync(cancellationToken);
        var exports = await db.ProblemPackageExports.AsNoTracking().Where(x => objectIds.Contains(x.ObjectId))
            .Select(x => x.ObjectId).ToListAsync(cancellationToken);
        var statements = await db.ProblemStatements.AsNoTracking().Where(x => objectIds.Contains(x.ObjectId))
            .Select(x => x.ObjectId).ToListAsync(cancellationToken);
        var assets = await db.ProblemAssets.AsNoTracking().Where(x => objectIds.Contains(x.ObjectId))
            .Select(x => x.ObjectId).ToListAsync(cancellationToken);
        var references = await db.ReferenceSolutionSources.AsNoTracking().Where(x => objectIds.Contains(x.ObjectId))
            .Select(x => x.ObjectId).ToListAsync(cancellationToken);
        return testInputs.Concat(testOutputs).Concat(exports).Concat(statements).Concat(assets).Concat(references).ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlySet<string>> FindRetainedStagedAsync(IReadOnlyCollection<string> objectIds, CancellationToken cancellationToken)
    {
        var inputs = await db.GeneratedTests.AsNoTracking().Join(db.GeneratedTestSets.Where(x => !x.Published), x => x.TestSetId, x => x.Id, (test, _) => test)
            .Where(x => objectIds.Contains(x.InputObjectId)).Select(x => x.InputObjectId).ToListAsync(cancellationToken);
        var outputs = await db.GeneratedTests.AsNoTracking().Join(db.GeneratedTestSets.Where(x => !x.Published), x => x.TestSetId, x => x.Id, (test, _) => test)
            .Where(x => x.OutputObjectId != null && objectIds.Contains(x.OutputObjectId)).Select(x => x.OutputObjectId!).ToListAsync(cancellationToken);
        return inputs.Concat(outputs).ToHashSet(StringComparer.Ordinal);
    }
}
