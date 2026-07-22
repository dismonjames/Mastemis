using Mastemis.Application;
using Mastemis.Domain;
using Mastemis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Tests.Judge;

public sealed class JudgementReportingTests
{
    [Fact]
    public async Task Persists_bounded_result_metadata_and_outbox_atomically()
    {
        var now = DateTimeOffset.UtcNow; var worker = JudgeWorkerId.New(); var submission = SubmissionId.New();
        var job = JudgeJobId.New(); var lease = Guid.NewGuid();
        await using var db = CreateContext();
        db.Submissions.Add(new() { Id = submission.Value, SessionId = Guid.NewGuid(), ProblemId = Guid.NewGuid(), RevisionId = Guid.NewGuid(), Language = "cpp", CreatedAtUtc = now });
        db.JudgeJobs.Add(new()
        {
            Id = job.Value,
            SubmissionId = submission.Value,
            State = (int)JudgeJobState.Running,
            WorkerId = worker.Value,
            LeaseId = lease,
            LeaseExpiresAtUtc = now.AddMinutes(1),
            CreatedAtUtc = now,
            AvailableAtUtc = now
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var queue = new PostgresWorkerJudgeQueue(db, new FixedClock(now));
        var judgement = new Judgement(submission, SubmissionState.Accepted, 100, now);

        await queue.CompleteDetailedAsync(worker, job, lease, new(judgement, null, 23, 4096, 0, null, 7, 0,
            "compiler.ok", null, null, "podman", worker, "judge/1"), TestContext.Current.CancellationToken);

        var stored = await db.Judgements.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(23, stored.ExecutionMilliseconds); Assert.Equal(worker.Value, stored.WorkerId);
        Assert.Equal("podman", stored.SandboxBackend); Assert.Equal(2, await db.OutboxMessages.CountAsync(TestContext.Current.CancellationToken));
    }

    private static MastemisDbContext CreateContext() => new(new DbContextOptionsBuilder<MastemisDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow => now; }
}
