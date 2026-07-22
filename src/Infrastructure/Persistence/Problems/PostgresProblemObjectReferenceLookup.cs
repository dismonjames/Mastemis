using Mastemis.Application.Problems.Assets;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class PostgresProblemObjectReferenceLookup(MastemisDbContext db) : IProblemObjectReferenceLookup
{
    public async Task<IReadOnlySet<string>> FindReferencedAsync(IReadOnlyCollection<string> objectIds,
        CancellationToken cancellationToken)
    {
        if (objectIds.Count == 0) return new HashSet<string>(StringComparer.Ordinal);
        var testInputs = await db.GeneratedTests.AsNoTracking().Where(x => objectIds.Contains(x.InputObjectId))
            .Select(x => x.InputObjectId).ToListAsync(cancellationToken);
        var testOutputs = await db.GeneratedTests.AsNoTracking().Where(x => x.OutputObjectId != null && objectIds.Contains(x.OutputObjectId))
            .Select(x => x.OutputObjectId!).ToListAsync(cancellationToken);
        var exports = await db.ProblemPackageExports.AsNoTracking().Where(x => objectIds.Contains(x.ObjectId))
            .Select(x => x.ObjectId).ToListAsync(cancellationToken);
        return testInputs.Concat(testOutputs).Concat(exports).ToHashSet(StringComparer.Ordinal);
    }
}
