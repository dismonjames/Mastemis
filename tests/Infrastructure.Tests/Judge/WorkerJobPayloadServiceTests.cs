using Mastemis.Application;
using Mastemis.Contracts.Judge;
using Mastemis.Domain;
using Mastemis.Infrastructure.Persistence;
using Mastemis.Infrastructure.Persistence.Queue;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Tests.Judge;

public sealed class WorkerJobPayloadServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"mastemis-payload-{Guid.NewGuid():N}");
    private readonly string _databaseName = Guid.NewGuid().ToString("N");

    [Fact]
    public async Task Returns_only_payload_owned_by_current_worker_lease()
    {
        var now = DateTimeOffset.UtcNow; var worker = JudgeWorkerId.New(); var lease = Guid.NewGuid();
        var ids = await SeedAsync(worker, lease, now);
        await using var db = CreateContext();
        var service = new WorkerJobPayloadService(db, new FixedClock(now), new(_root, _root));

        var contract = await service.GetContractAsync(worker, ids.JobId, lease, TestContext.Current.CancellationToken);
        await using var source = await service.OpenSourceAsync(worker, ids.JobId, lease, TestContext.Current.CancellationToken);

        Assert.Equal("cpp", contract.LanguageId);
        Assert.Single(contract.Tests);
        Assert.Equal(4, source.Length);
        await Assert.ThrowsAsync<ApplicationFailure>(() => service.GetContractAsync(
            JudgeWorkerId.New(), ids.JobId, lease, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rejects_unsafe_test_object_identifier()
    {
        var now = DateTimeOffset.UtcNow; var worker = JudgeWorkerId.New(); var lease = Guid.NewGuid();
        var ids = await SeedAsync(worker, lease, now);
        await using (var db = CreateContext())
        {
            var test = await db.ProblemTestCases.SingleAsync(TestContext.Current.CancellationToken);
            test.InputObjectId = "../../etc/passwd"; await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var read = CreateContext();
        var service = new WorkerJobPayloadService(read, new FixedClock(now), new(_root, _root));
        await Assert.ThrowsAsync<ApplicationFailure>(() => service.OpenTestDataAsync(worker, ids.JobId, lease, 1,
            false, TestContext.Current.CancellationToken));
    }

    private async Task<(JudgeJobId JobId, SubmissionId SubmissionId)> SeedAsync(JudgeWorkerId worker, Guid lease, DateTimeOffset now)
    {
        Directory.CreateDirectory(Path.Combine(_root, "source")); Directory.CreateDirectory(Path.Combine(_root, "judge"));
        var revision = SourceRevisionId.New(); var sourceObject = $"source/{revision.Value:N}.bin";
        var inputObject = $"judge/{Guid.NewGuid():N}.bin"; var expectedObject = $"judge/{Guid.NewGuid():N}.bin";
        await File.WriteAllBytesAsync(Path.Combine(_root, sourceObject), [1, 2, 3, 4], TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(_root, inputObject), [1], TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(_root, expectedObject), [2], TestContext.Current.CancellationToken);
        var submission = SubmissionId.New(); var job = JudgeJobId.New(); var problem = ProblemId.New();
        await using var db = CreateContext();
        db.SourceRevisions.Add(new() { Id = revision.Value, SessionId = Guid.NewGuid(), ObjectId = sourceObject, Sha256 = new('a', 64), Length = 4, CreatedAtUtc = now });
        db.Submissions.Add(new() { Id = submission.Value, SessionId = Guid.NewGuid(), ProblemId = problem.Value, RevisionId = revision.Value, Language = "cpp", CreatedAtUtc = now });
        db.JudgeJobs.Add(new() { Id = job.Value, SubmissionId = submission.Value, State = (int)JudgeJobState.Claimed, WorkerId = worker.Value, LeaseId = lease, LeaseExpiresAtUtc = now.AddMinutes(1), CreatedAtUtc = now, AvailableAtUtc = now });
        db.ProblemJudgeProfiles.Add(new() { ProblemId = problem.Value, CpuMilliseconds = 1000, WallMilliseconds = 2000, MemoryBytes = 64 * 1024 * 1024, OutputBytes = 1024, FileBytes = 2048, ProcessCount = 4, TestCount = 1, CompilationMilliseconds = 5000, CompilationOutputBytes = 4096 });
        db.ProblemTestCases.Add(new() { Id = Guid.NewGuid(), ProblemId = problem.Value, TestIndex = 1, InputObjectId = inputObject, ExpectedObjectId = expectedObject, InputBytes = 1, ExpectedBytes = 1, CheckerId = "exact" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken); return (job, submission);
    }

    private MastemisDbContext CreateContext() => new(new DbContextOptionsBuilder<MastemisDbContext>()
        .UseInMemoryDatabase("payload-" + _databaseName).Options);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow => now; }
}
