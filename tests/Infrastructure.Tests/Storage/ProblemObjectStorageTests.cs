using Mastemis.Application;
using Mastemis.Application.Problems.Assets;
using Mastemis.Infrastructure.Storage.ProblemObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mastemis.Infrastructure.Tests.Storage;

public sealed class ProblemObjectStorageTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"mastemis-problem-objects-{Guid.NewGuid():N}");
    private readonly TestClock _clock = new();

    [Fact]
    public async Task Stages_hashes_commits_and_reads_generated_object()
    {
        var storage = new FileProblemObjectStorage(_root, _clock);
        var staged = await storage.StageAsync(ProblemObjectKind.TestInput, new MemoryStream("input\n"u8.ToArray()), 100,
            TestContext.Current.CancellationToken);
        Assert.StartsWith("problem/test-input/", staged.ObjectId, StringComparison.Ordinal);
        Assert.Equal(6, staged.Length);
        await Assert.ThrowsAsync<ApplicationFailure>(() => storage.OpenReadAsync(staged.ObjectId, 100, TestContext.Current.CancellationToken));
        await storage.MarkReferencedAsync(staged.ObjectId, TestContext.Current.CancellationToken);
        await using var content = await storage.OpenReadAsync(staged.ObjectId, 100, TestContext.Current.CancellationToken);
        using var reader = new StreamReader(content);
        Assert.Equal("input\n", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        await storage.MarkReferencedAsync(staged.ObjectId, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Rejects_oversize_and_unsafe_identifiers()
    {
        var storage = new FileProblemObjectStorage(_root, _clock);
        await Assert.ThrowsAsync<ApplicationFailure>(() => storage.StageAsync(ProblemObjectKind.Asset,
            new MemoryStream(new byte[11]), 10, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ApplicationFailure>(() => storage.OpenReadAsync("../../secret", 10, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reconciliation_removes_only_stale_staged_objects()
    {
        var storage = new FileProblemObjectStorage(_root, _clock);
        var stale = await storage.StageAsync(ProblemObjectKind.Package, new MemoryStream([1]), 10, TestContext.Current.CancellationToken);
        var recent = await storage.StageAsync(ProblemObjectKind.Package, new MemoryStream([2]), 10, TestContext.Current.CancellationToken);
        var stalePath = Path.Combine(_root, ".staged", "package", $"{stale.ObjectId.Split('/')[2]}.bin");
        File.SetLastWriteTimeUtc(stalePath, DateTime.UtcNow.AddDays(-2));
        var reconciler = new ProblemObjectReconciler(_root, storage, new NoReferences(), NullLogger<ProblemObjectReconciler>.Instance);
        var result = await reconciler.ReconcileAsync(DateTimeOffset.UtcNow.AddDays(-1), 100, TestContext.Current.CancellationToken);
        Assert.Equal(new(1, 1), result);
        await storage.MarkReferencedAsync(recent.ObjectId, TestContext.Current.CancellationToken);
        await storage.DeleteStagedAsync(stale.ObjectId, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        return ValueTask.CompletedTask;
    }

    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
    private sealed class NoReferences : IProblemObjectReferenceLookup
    {
        public Task<IReadOnlySet<string>> FindReferencedAsync(IReadOnlyCollection<string> objectIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
    }
}
