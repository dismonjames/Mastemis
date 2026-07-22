using Mastemis.Application.Problems.Assets;
using Mastemis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mastemis.Infrastructure.Storage.ProblemObjects.Exports;

public sealed class ProblemExportCleanupService(MastemisDbContext db, IProblemObjectStorage storage,
    ProblemExportOptions options, ILogger<ProblemExportCleanupService> logger)
{
    public async Task<int> CleanupAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var candidates = await db.ProblemPackageExports.Where(x => x.ObjectId != string.Empty &&
                (x.Status == "Expired" || x.Status == "Failed" || x.ExpiresAtUtc <= now)).OrderBy(x => x.ExpiresAtUtc)
            .Take(options.BoundedBatchSize).ToArrayAsync(cancellationToken);
        var removed = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var verified = await db.ProblemPackageExports.SingleOrDefaultAsync(x => x.Id == candidate.Id &&
                x.ObjectId == candidate.ObjectId &&
                (x.Status == "Expired" || x.Status == "Failed" || x.ExpiresAtUtc <= now), cancellationToken);
            if (verified is null) continue;
            await storage.DeleteStagedAsync(verified.ObjectId, cancellationToken);
            await storage.DeleteReferencedAsync(verified.ObjectId, cancellationToken);
            verified.ObjectId = string.Empty;
            verified.Status = "CleanupEligible";
            removed++;
        }
        if (removed > 0) await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Expired problem export cleanup removed {RemovedCount} objects.", removed);
        return removed;
    }
}
