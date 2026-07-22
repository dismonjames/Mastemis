using Mastemis.Application.Problems.Assets;
using Microsoft.Extensions.Logging;

namespace Mastemis.Infrastructure.Storage.ProblemObjects;

public sealed record ProblemObjectReconciliationResult(int Scanned, int Removed);

public sealed class ProblemObjectReconciler(string rootPath, IProblemObjectStorage storage, IProblemObjectReferenceLookup references,
    ILogger<ProblemObjectReconciler> logger)
{
    private readonly string _root = Path.GetFullPath(rootPath);

    public async Task<ProblemObjectReconciliationResult> ReconcileAsync(DateTimeOffset olderThanUtc, int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 10_000) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var stagedRoot = Path.Combine(_root, ".staged");
        if (!Directory.Exists(stagedRoot)) return new(0, 0);
        var candidates = Directory.EnumerateFiles(stagedRoot, "*.bin", SearchOption.AllDirectories)
            .Where(path => File.GetLastWriteTimeUtc(path) <= olderThanUtc.UtcDateTime)
            .OrderBy(path => path, StringComparer.Ordinal).Take(batchSize).ToArray();
        var identified = candidates.Select(path => (Path: path, ObjectId: GetObjectId(stagedRoot, path)))
            .Where(item => item.ObjectId is not null).Select(item => (item.Path, ObjectId: item.ObjectId!)).ToArray();
        var referenced = await references.FindReferencedAsync(identified.Select(item => item.ObjectId).ToArray(), cancellationToken);
        var removed = 0;
        foreach (var (_, objectId) in identified)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (referenced.Contains(objectId)) { await storage.MarkReferencedAsync(objectId, cancellationToken); continue; }
            await storage.DeleteStagedAsync(objectId, cancellationToken);
            removed++;
        }
        logger.LogInformation("Problem object reconciliation scanned {ScannedCount} objects and removed {RemovedCount} stale staged objects.", candidates.Length, removed);
        return new(candidates.Length, removed);
    }

    private static string? GetObjectId(string stagedRoot, string path)
    {
        var relative = Path.GetRelativePath(stagedRoot, path).Replace(Path.DirectorySeparatorChar, '/');
        var separator = relative.IndexOf('/');
        var objectId = separator > 0 ? $"problem/{relative[..separator]}/{Path.GetFileNameWithoutExtension(relative)}" : string.Empty;
        return ProblemObjectPath.TryParse(objectId, out _, out _) ? objectId : null;
    }
}
