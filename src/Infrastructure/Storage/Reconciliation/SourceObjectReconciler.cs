using Mastemis.Application;
using Mastemis.Infrastructure.Persistence;
using Mastemis.Infrastructure.Storage.SourceRevisions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mastemis.Infrastructure.Storage.Reconciliation;

public sealed record SourceReconciliationResult(int Scanned, int Retained, int Removed);

public sealed class SourceObjectReconciler(MastemisDbContext db, SourceReconciliationOptions options, IClock clock,
    SourceReconciliationStatus status, ILogger<SourceObjectReconciler> logger)
{
    public async Task<SourceReconciliationResult> ReconcileAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); var sourceRoot = Path.Combine(Path.GetFullPath(options.RootPath), "source");
        if (!Directory.Exists(sourceRoot)) { status.MarkSuccess(clock.UtcNow); return new(0, 0, 0); }
        var cutoff = clock.UtcNow - options.OrphanAge;
        var candidates = Directory.EnumerateFiles(sourceRoot, "*.bin", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, ObjectId = $"source/{Path.GetFileName(path)}", Modified = File.GetLastWriteTimeUtc(path) })
            .Where(x => x.Modified <= cutoff && SourceObjectPath.IsGeneratedSourceObject(x.ObjectId))
            .OrderBy(x => x.Modified).Take(options.BoundedBatchSize).ToArray();
        if (candidates.Length == 0) { status.MarkSuccess(clock.UtcNow); return new(0, 0, 0); }

        var ids = candidates.Select(x => x.ObjectId).ToArray();
        var referenced = (await db.SourceRevisions.AsNoTracking().Where(x => ids.Contains(x.ObjectId))
            .Select(x => x.ObjectId).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var removed = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (referenced.Contains(candidate.ObjectId)) continue;
            try { File.Delete(SourceObjectPath.Resolve(options.RootPath, candidate.ObjectId)); removed++; }
            catch (FileNotFoundException) { }
        }
        status.MarkSuccess(clock.UtcNow);
        logger.LogInformation("Source object reconciliation scanned {ScannedCount} objects and removed {RemovedCount} stale unreferenced objects.", candidates.Length, removed);
        return new(candidates.Length, candidates.Length - removed, removed);
    }
}
