using Mastemis.Contracts.Judge;

namespace Mastemis.Judge.Workspaces;

public sealed class JudgeWorkspace : IAsyncDisposable
{
    private readonly Func<string, CancellationToken, ValueTask> _cleanup;
    private int _disposed;

    internal JudgeWorkspace(string root, Func<string, CancellationToken, ValueTask> cleanup)
    {
        Root = root; _cleanup = cleanup;
        SourceDirectory = Path.Combine(root, "source"); BuildDirectory = Path.Combine(root, "build");
        RunDirectory = Path.Combine(root, "run"); InputDirectory = Path.Combine(root, "input");
        OutputDirectory = Path.Combine(root, "output"); TemporaryDirectory = Path.Combine(root, "tmp");
    }

    public string Root { get; }
    public string SourceDirectory { get; }
    public string BuildDirectory { get; }
    public string RunDirectory { get; }
    public string InputDirectory { get; }
    public string OutputDirectory { get; }
    public string TemporaryDirectory { get; }

    public async ValueTask<IReadOnlyList<MaterializedSource>> MaterializeSourcesAsync(
        IReadOnlyList<SourceFile> sources, IReadOnlySet<string> allowedExtensions, CancellationToken cancellationToken)
    {
        if (sources.Count is < 1 or > 32) throw Unsafe("Source count is outside the supported range.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            SourceNameValidator.Validate(source.FileName, allowedExtensions);
            if (!names.Add(source.FileName.Normalize())) throw Unsafe("Duplicate normalized source filename.");
        }
        var materialized = new List<MaterializedSource>(sources.Count);
        for (var index = 0; index < sources.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested(); var source = sources[index];
            var extension = Path.GetExtension(source.FileName).ToLowerInvariant();
            var path = Path.Combine(SourceDirectory, $"source_{index + 1:D3}{extension}");
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await stream.WriteAsync(source.Content, cancellationToken);
            materialized.Add(new(source.FileName, path));
        }
        return materialized;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await _cleanup(Root, CancellationToken.None);
    }
    private static JudgeContractException Unsafe(string message) => new(JudgeFailureCode.UnsafeSourceName, message);
}
