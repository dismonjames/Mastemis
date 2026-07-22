using Mastemis.Application;
using Mastemis.Infrastructure.Persistence;
using Mastemis.Infrastructure.Storage.Reconciliation;
using Mastemis.Infrastructure.Storage.SourceRevisions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mastemis.Infrastructure.Tests.Storage;

public sealed class SourceObjectReconcilerTests
{
    [Fact]
    public async Task Referenced_object_is_retained_while_stale_orphan_is_removed()
    {
        await using var fixture = await Fixture.CreateAsync();
        var referenced = await fixture.CreateObjectAsync(stale: true); var orphan = await fixture.CreateObjectAsync(stale: true);
        fixture.Db.SourceRevisions.Add(new SourceRevisionRow
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ObjectId = referenced,
            Sha256 = new string('0', 64),
            Length = 1,
            CreatedAtUtc = FixedClock.Now
        });
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await fixture.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Removed); Assert.True(fixture.Exists(referenced)); Assert.False(fixture.Exists(orphan));
    }

    [Fact]
    public async Task Recent_staged_object_is_retained_and_missing_objects_are_tolerated()
    {
        await using var fixture = await Fixture.CreateAsync(); var recent = await fixture.CreateObjectAsync(stale: false);
        var result = await fixture.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, result.Removed); Assert.True(fixture.Exists(recent));
        File.Delete(SourceObjectPath.Resolve(fixture.Root, recent));
        var repeat = await fixture.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, repeat.Removed);
    }

    [Fact]
    public async Task Repeated_cleanup_is_idempotent()
    {
        await using var fixture = await Fixture.CreateAsync(); _ = await fixture.CreateObjectAsync(stale: true);
        Assert.Equal(1, (await fixture.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken)).Removed);
        Assert.Equal(0, (await fixture.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken)).Removed);
    }

    [Fact]
    public async Task Cancellation_prevents_scanning()
    {
        await using var fixture = await Fixture.CreateAsync(); _ = await fixture.CreateObjectAsync(stale: true);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Reconciler.ReconcileAsync(cancellation.Token));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("/absolute/path")]
    [InlineData("source/../../outside")]
    public void Unsafe_object_paths_are_rejected(string objectId) =>
        Assert.Throws<ApplicationFailure>(() => SourceObjectPath.Resolve(Path.GetTempPath(), objectId));

    [Fact]
    public async Task Database_failure_does_not_delete_orphan_candidates()
    {
        await using var fixture = await Fixture.CreateAsync(); var candidate = await fixture.CreateObjectAsync(stale: true);
        await fixture.Db.DisposeAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken));
        Assert.True(fixture.Exists(candidate));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(string root, MastemisDbContext db)
        {
            Root = root; Db = db;
            Reconciler = new(db, new(root, TimeSpan.FromHours(1), TimeSpan.FromHours(1), 100), new FixedClock(),
                new SourceReconciliationStatus(), NullLogger<SourceObjectReconciler>.Instance);
        }
        public string Root { get; }
        public MastemisDbContext Db { get; }
        public SourceObjectReconciler Reconciler { get; }
        public static Task<Fixture> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), $"mastemis-reconciliation-{Guid.NewGuid():N}"); Directory.CreateDirectory(root);
            var db = new MastemisDbContext(new DbContextOptionsBuilder<MastemisDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            return Task.FromResult(new Fixture(root, db));
        }
        public async Task<string> CreateObjectAsync(bool stale)
        {
            var objectId = $"source/{Guid.NewGuid():N}.bin"; var path = SourceObjectPath.Resolve(Root, objectId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllBytesAsync(path, [1], TestContext.Current.CancellationToken);
            File.SetLastWriteTimeUtc(path, stale ? FixedClock.Now.AddDays(-2).UtcDateTime : FixedClock.Now.UtcDateTime); return objectId;
        }
        public bool Exists(string objectId) => File.Exists(SourceObjectPath.Resolve(Root, objectId));
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    private sealed class FixedClock : IClock { public static DateTimeOffset Now { get; } = new(2026, 7, 22, 8, 0, 0, TimeSpan.Zero); public DateTimeOffset UtcNow => Now; }
}
