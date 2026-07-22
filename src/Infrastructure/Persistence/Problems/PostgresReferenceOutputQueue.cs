using System.Text.Json;
using Mastemis.Application;
using Mastemis.Contracts.Problems.ReferenceOutputs;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class PostgresReferenceOutputQueue(MastemisDbContext db, IClock clock) : IReferenceOutputQueue
{
    public async Task<Guid> EnqueueAsync(ReferenceOutputJobPayload payload, int maximumAttempts, CancellationToken cancellationToken)
    {
        payload.Validate(); if (maximumAttempts is < 1 or > 10) throw Invalid("Reference job retry limit is invalid.");
        var existing = await db.ReferenceOutputJobs.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == payload.OperationId, cancellationToken);
        if (existing is not null) return existing.Id;
        var row = new ReferenceOutputJobRow
        {
            Id = payload.JobId,
            OperationId = payload.OperationId,
            ProblemId = payload.ProblemId.Value,
            Language = payload.Language,
            ContractVersion = payload.ContractVersion,
            PayloadJson = JsonSerializer.Serialize(payload),
            Status = (int)ReferenceOutputJobStatus.Pending,
            MaximumAttempts = maximumAttempts,
            CreatedAtUtc = clock.UtcNow,
            AvailableAtUtc = clock.UtcNow,
            ConcurrencyToken = Guid.NewGuid()
        };
        db.ReferenceOutputJobs.Add(row); db.OutboxMessages.Add(ProblemOutbox.Create("ReferenceOutputJobQueued", payload.ProblemId.Value,
            clock.UtcNow, new { problemId = payload.ProblemId.Value, operationId = payload.OperationId, jobId = payload.JobId }));
        try { await db.SaveChangesAsync(cancellationToken); return row.Id; }
        catch (DbUpdateException) { return await db.ReferenceOutputJobs.AsNoTracking().Where(x => x.OperationId == payload.OperationId).Select(x => x.Id).SingleAsync(cancellationToken); }
    }

    public async Task<ReferenceOutputJobLease?> ClaimAsync(JudgeWorkerId workerId, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        ValidateLease(leaseDuration);
        var worker = await db.JudgeWorkers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == workerId.Value && x.IsEnabled, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Worker not found.");
        var languages = JsonSerializer.Deserialize<string[]>(worker.LanguagesJson) ?? [];
        if (languages.Length == 0) return null;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken); var now = clock.UtcNow;
        var row = await db.ReferenceOutputJobs.FromSqlInterpolated($$"""
            SELECT * FROM reference_output_jobs
            WHERE (("Status" = {{(int)ReferenceOutputJobStatus.Pending}} AND "AvailableAtUtc" <= {{now}})
                OR ("Status" IN ({{(int)ReferenceOutputJobStatus.Claimed}}, {{(int)ReferenceOutputJobStatus.Running}}) AND "LeaseExpiresAtUtc" <= {{now}}))
              AND "Attempt" < "MaximumAttempts" AND "Language" = ANY({{languages}})
            ORDER BY "CreatedAtUtc" FOR UPDATE SKIP LOCKED LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);
        if (row is null) { await transaction.CommitAsync(cancellationToken); return null; }
        row.Status = (int)ReferenceOutputJobStatus.Claimed; row.WorkerId = workerId.Value; row.LeaseToken = Guid.NewGuid();
        row.LeaseExpiresAtUtc = now + leaseDuration; row.Attempt++; row.ConcurrencyToken = Guid.NewGuid();
        db.OutboxMessages.Add(ProblemOutbox.Create("ReferenceOutputJobClaimed", row.ProblemId, now,
            new { problemId = row.ProblemId, operationId = row.OperationId, jobId = row.Id, workerId = workerId.Value }));
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return new(row.Id, row.OperationId, workerId, row.LeaseToken.Value, row.LeaseExpiresAtUtc.Value, row.Attempt, row.MaximumAttempts);
    }

    public async Task RenewAsync(JudgeWorkerId workerId, Guid jobId, Guid leaseToken, TimeSpan leaseDuration, CancellationToken cancellationToken)
    { ValidateLease(leaseDuration); var row = await OwnedAsync(workerId, jobId, leaseToken, cancellationToken); row.LeaseExpiresAtUtc = clock.UtcNow + leaseDuration; row.ConcurrencyToken = Guid.NewGuid(); await db.SaveChangesAsync(cancellationToken); }
    public async Task StartAsync(JudgeWorkerId workerId, Guid jobId, Guid leaseToken, CancellationToken cancellationToken)
    { var row = await OwnedAsync(workerId, jobId, leaseToken, cancellationToken); row.Status = (int)ReferenceOutputJobStatus.Running; row.StartedAtUtc ??= clock.UtcNow; row.ConcurrencyToken = Guid.NewGuid(); await db.SaveChangesAsync(cancellationToken); }
    public async Task<ReferenceOutputJobPayload> GetPayloadAsync(JudgeWorkerId workerId, Guid jobId, Guid leaseToken, CancellationToken cancellationToken)
    { var row = await OwnedAsync(workerId, jobId, leaseToken, cancellationToken); return JsonSerializer.Deserialize<ReferenceOutputJobPayload>(row.PayloadJson) ?? throw Invalid("Reference job payload is invalid."); }

    public async Task CompleteAsync(ReferenceOutputCompletion completion, CancellationToken cancellationToken)
    {
        var existing = await db.ReferenceOutputJobs.SingleOrDefaultAsync(x => x.Id == completion.JobId, cancellationToken) ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Reference job not found.");
        if (existing.Status == (int)ReferenceOutputJobStatus.Completed && existing.WorkerId == completion.WorkerId.Value) return;
        var row = await OwnedAsync(completion.WorkerId, completion.JobId, completion.LeaseToken, cancellationToken);
        if (row.OperationId != completion.OperationId) throw LeaseFailure();
        row.Status = (int)ReferenceOutputJobStatus.Completed; row.CompletedAtUtc = clock.UtcNow; row.LeaseExpiresAtUtc = null; row.ConcurrencyToken = Guid.NewGuid();
        db.OutboxMessages.Add(ProblemOutbox.Create("ReferenceOutputCompleted", row.ProblemId, clock.UtcNow,
            new { problemId = row.ProblemId, operationId = row.OperationId, jobId = row.Id, completedTests = completion.CompletedTests }));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(ReferenceOutputFailure failure, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(failure.FailureCode) || failure.FailureCode.Length > 100) throw Invalid("Reference failure code is invalid.");
        var row = await OwnedAsync(failure.WorkerId, failure.JobId, failure.LeaseToken, cancellationToken);
        if (row.OperationId != failure.OperationId) throw LeaseFailure(); row.FailureCode = failure.FailureCode;
        if (row.Attempt >= row.MaximumAttempts) { row.Status = (int)ReferenceOutputJobStatus.Failed; row.CompletedAtUtc = clock.UtcNow; }
        else { row.Status = (int)ReferenceOutputJobStatus.Pending; row.AvailableAtUtc = clock.UtcNow.AddSeconds(Math.Pow(2, row.Attempt)); }
        row.WorkerId = null; row.LeaseToken = null; row.LeaseExpiresAtUtc = null; row.ConcurrencyToken = Guid.NewGuid(); await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(Guid operationId, CancellationToken cancellationToken)
    { var row = await db.ReferenceOutputJobs.SingleOrDefaultAsync(x => x.OperationId == operationId, cancellationToken); if (row is null || row.Status is (int)ReferenceOutputJobStatus.Completed or (int)ReferenceOutputJobStatus.Cancelled) return; row.Status = (int)ReferenceOutputJobStatus.Cancelled; row.CompletedAtUtc = clock.UtcNow; row.LeaseExpiresAtUtc = null; row.ConcurrencyToken = Guid.NewGuid(); await db.SaveChangesAsync(cancellationToken); }

    private async Task<ReferenceOutputJobRow> OwnedAsync(JudgeWorkerId worker, Guid jobId, Guid lease, CancellationToken ct)
    { var row = await db.ReferenceOutputJobs.SingleOrDefaultAsync(x => x.Id == jobId, ct) ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Reference job not found."); if (row.WorkerId != worker.Value || row.LeaseToken != lease || row.LeaseExpiresAtUtc <= clock.UtcNow || row.Status is not ((int)ReferenceOutputJobStatus.Claimed) and not ((int)ReferenceOutputJobStatus.Running)) throw LeaseFailure(); return row; }
    private static void ValidateLease(TimeSpan lease) { if (lease < TimeSpan.FromSeconds(5) || lease > TimeSpan.FromMinutes(30)) throw Invalid("Reference job lease duration is invalid."); }
    private static ApplicationFailure Invalid(string message) => new(ErrorCodes.InvalidInput, message);
    private static ApplicationFailure LeaseFailure() => new(ErrorCodes.LeaseRejected, "Worker does not own the active reference job lease.");
}
