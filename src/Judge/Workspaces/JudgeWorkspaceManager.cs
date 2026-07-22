namespace Mastemis.Judge.Workspaces;

public sealed class JudgeWorkspaceManager(string rootPath) : IJudgeWorkspaceManager
{
    private readonly string _root = Path.GetFullPath(rootPath);

    public ValueTask<JudgeWorkspace> CreateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); Directory.CreateDirectory(_root);
        var path = Resolve($"job-{Guid.NewGuid():N}"); Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        foreach (var name in new[] { "source", "build", "run", "input", "output", "tmp" }) Directory.CreateDirectory(Path.Combine(path, name));
        return ValueTask.FromResult(new JudgeWorkspace(path, CleanupAsync));
    }

    public async ValueTask<int> ReconcileStaleAsync(TimeSpan minimumAge, int batchSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (minimumAge <= TimeSpan.Zero || batchSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(minimumAge));
        if (!Directory.Exists(_root)) return 0;
        var cutoff = DateTime.UtcNow - minimumAge; var removed = 0;
        var directories = Directory.EnumerateDirectories(_root, "job-*", SearchOption.TopDirectoryOnly)
            .Select(path => new DirectoryInfo(path)).Where(info => info.Name.Length == 36 &&
                Guid.TryParseExact(info.Name[4..], "N", out _) && info.LastWriteTimeUtc <= cutoff)
            .OrderBy(info => info.LastWriteTimeUtc).Take(batchSize).ToArray();
        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            await CleanupAsync(directory.FullName, cancellationToken); removed++;
        }
        return removed;
    }

    private async ValueTask CleanupAsync(string path, CancellationToken cancellationToken)
    {
        var canonical = Resolve(Path.GetFileName(path));
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { if (Directory.Exists(canonical)) Directory.Delete(canonical, true); return; }
            catch (IOException) when (attempt < 3) { await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), cancellationToken); }
            catch (UnauthorizedAccessException) when (attempt < 3) { await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), cancellationToken); }
        }
    }

    private string Resolve(string child)
    {
        var path = Path.GetFullPath(Path.Combine(_root, child));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe workspace path.");
        return path;
    }
}
