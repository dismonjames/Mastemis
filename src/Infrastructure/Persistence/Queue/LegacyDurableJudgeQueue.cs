using Mastemis.Application;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence;

public sealed class LegacyDurableJudgeQueue(MastemisDbContext db, IClock clock) : IDurableJudgeQueue
{
    public Task EnqueueAsync(JudgeJob job, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!db.JudgeJobs.Local.Any(x => x.Id == job.Id.Value)) db.JudgeJobs.Add(PersistenceMapper.ToRow(job));
        return Task.CompletedTask;
    }

    public async Task<JudgeJob?> ClaimAsync(JudgeWorkerId workerId, TimeSpan lease, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.UtcNow;
        var row = await db.JudgeJobs.FromSqlInterpolated($$"""
            SELECT * FROM judge_jobs
            WHERE (("State" = {{(int)JudgeJobState.Pending}} AND "AvailableAtUtc" <= {{now}})
                OR ("State" = {{(int)JudgeJobState.Claimed}} AND "LeaseExpiresAtUtc" <= {{now}}))
              AND "Attempt" < "MaximumAttempts"
            ORDER BY "Priority" DESC, "CreatedAtUtc" FOR UPDATE SKIP LOCKED LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);
        if (row is null) { await transaction.CommitAsync(cancellationToken); return null; }
        row.State = (int)JudgeJobState.Claimed; row.WorkerId = workerId.Value; row.LeaseId = Guid.NewGuid();
        row.LeaseExpiresAtUtc = now + lease; row.Attempt++; row.ConcurrencyToken = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return PersistenceMapper.ToDomain(row);
    }

    public async Task CompleteAsync(JudgeJobId jobId, Judgement judgement, CancellationToken cancellationToken)
    {
        var row = await db.JudgeJobs.SingleOrDefaultAsync(x => x.Id == jobId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Judge job not found.");
        if (row.SubmissionId != judgement.SubmissionId.Value) throw new ApplicationFailure(ErrorCodes.LeaseRejected, "The judgement does not match this job.");
        if (row.State == (int)JudgeJobState.Completed) return;
        row.State = (int)JudgeJobState.Completed; row.CompletedAtUtc = judgement.CompletedAtUtc;
        row.LeaseExpiresAtUtc = null; row.ConcurrencyToken = Guid.NewGuid();
        db.Judgements.Add(new JudgementRow
        {
            SubmissionId = judgement.SubmissionId.Value,
            Verdict = (int)judgement.Verdict,
            Score = judgement.Score,
            CompletedAtUtc = judgement.CompletedAtUtc
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
