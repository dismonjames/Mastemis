using System.Security.Cryptography;
using System.Text.Json;
using Mastemis.Application;
using Mastemis.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence;

public sealed class WorkerCredentialService(MastemisDbContext db, IClock clock, IPasswordHasher<WorkerCredentialRow> hasher)
    : IWorkerCredentialService
{
    public async Task<IssuedWorkerCredential> RegisterAsync(string name, int capacity, DateTimeOffset? expiresAtUtc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || capacity is < 1 or > 128)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Worker name or capacity is invalid.");
        var worker = new JudgeWorkerRow
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Capacity = capacity,
            IsEnabled = true,
            CreatedAtUtc = clock.UtcNow
        };
        db.JudgeWorkers.Add(worker);
        var issued = CreateCredential(worker.Id, expiresAtUtc);
        await db.SaveChangesAsync(cancellationToken);
        return issued;
    }

    public async Task<IssuedWorkerCredential> RotateAsync(JudgeWorkerId workerId, DateTimeOffset? expiresAtUtc, CancellationToken cancellationToken)
    {
        var worker = await db.JudgeWorkers.SingleOrDefaultAsync(x => x.Id == workerId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Worker not found.");
        var current = await db.WorkerCredentials.Where(x => x.WorkerId == worker.Id && x.RevokedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var credential in current) credential.RevokedAtUtc = clock.UtcNow;
        var issued = CreateCredential(worker.Id, expiresAtUtc);
        await db.SaveChangesAsync(cancellationToken);
        return issued;
    }

    public async Task RevokeAsync(JudgeWorkerId workerId, CancellationToken cancellationToken)
    {
        var worker = await db.JudgeWorkers.SingleOrDefaultAsync(x => x.Id == workerId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Worker not found.");
        worker.IsEnabled = false;
        var credentials = await db.WorkerCredentials.Where(x => x.WorkerId == worker.Id && x.RevokedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var credential in credentials) credential.RevokedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> AuthenticateAsync(JudgeWorkerId workerId, string secret, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length > 500) return false;
        var worker = await db.JudgeWorkers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == workerId.Value, cancellationToken);
        if (worker is null || !worker.IsEnabled) return false;
        var credentials = await db.WorkerCredentials.AsNoTracking().Where(x => x.WorkerId == worker.Id &&
            x.RevokedAtUtc == null && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > clock.UtcNow)).ToListAsync(cancellationToken);
        foreach (var credential in credentials)
            if (hasher.VerifyHashedPassword(credential, credential.SecretHash, secret) != PasswordVerificationResult.Failed) return true;
        return false;
    }

    public async Task HeartbeatAsync(JudgeWorkerId workerId, int capacity, CancellationToken cancellationToken)
    {
        if (capacity is < 1 or > 128) throw new ApplicationFailure(ErrorCodes.InvalidInput, "Worker capacity is invalid.");
        var worker = await db.JudgeWorkers.SingleOrDefaultAsync(x => x.Id == workerId.Value && x.IsEnabled, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Worker not found.");
        worker.Capacity = capacity; worker.LastHeartbeatUtc = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task HeartbeatAsync(JudgeWorkerId workerId, int capacity, IReadOnlyList<string> languages,
        string sandboxBackend, CancellationToken cancellationToken)
    {
        if (languages.Count is < 1 or > 16 || languages.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 32) ||
            string.IsNullOrWhiteSpace(sandboxBackend) || sandboxBackend.Length > 100)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Worker capabilities are invalid.");
        await HeartbeatAsync(workerId, capacity, cancellationToken);
        var worker = await db.JudgeWorkers.SingleAsync(x => x.Id == workerId.Value, cancellationToken);
        worker.LanguagesJson = JsonSerializer.Serialize(languages.Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray());
        worker.SandboxBackend = sandboxBackend;
        await db.SaveChangesAsync(cancellationToken);
    }

    private IssuedWorkerCredential CreateCredential(Guid workerId, DateTimeOffset? expiresAtUtc)
    {
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var credential = new WorkerCredentialRow
        {
            Id = Guid.NewGuid(),
            WorkerId = workerId,
            CreatedAtUtc = clock.UtcNow,
            ExpiresAtUtc = expiresAtUtc?.ToUniversalTime()
        };
        credential.SecretHash = hasher.HashPassword(credential, secret);
        db.WorkerCredentials.Add(credential);
        return new IssuedWorkerCredential(new JudgeWorkerId(workerId), secret, credential.ExpiresAtUtc);
    }
}

public sealed class PostgresWorkerJudgeQueue(MastemisDbContext db, IClock clock) : IWorkerJudgeQueue
{
    public async Task<WorkerJobLease?> ClaimAsync(JudgeWorkerId workerId, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        ValidateLease(leaseDuration);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.UtcNow;
        var row = await db.JudgeJobs.FromSqlInterpolated($$"""
            SELECT * FROM judge_jobs
            WHERE (("State" = {{(int)JudgeJobState.Pending}} AND "AvailableAtUtc" <= {{now}})
                OR ("State" = {{(int)JudgeJobState.Claimed}} AND "LeaseExpiresAtUtc" <= {{now}}))
              AND "Attempt" < "MaximumAttempts"
            ORDER BY "Priority" DESC, "CreatedAtUtc"
            FOR UPDATE SKIP LOCKED LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);
        if (row is null) { await transaction.CommitAsync(cancellationToken); return null; }
        row.State = (int)JudgeJobState.Claimed; row.WorkerId = workerId.Value; row.LeaseId = Guid.NewGuid();
        row.LeaseExpiresAtUtc = now + leaseDuration; row.Attempt++; row.ConcurrencyToken = Guid.NewGuid();
        AddOutbox(new JudgeJobClaimed(new JudgeJobId(row.Id), workerId));
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return new WorkerJobLease(new JudgeJobId(row.Id), new SubmissionId(row.SubmissionId), row.LeaseId.Value,
            row.LeaseExpiresAtUtc.Value, row.Attempt, row.MaximumAttempts);
    }

    public async Task RenewAsync(JudgeWorkerId workerId, JudgeJobId jobId, Guid leaseId, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        ValidateLease(leaseDuration);
        var row = await OwnedLeaseAsync(workerId, jobId, leaseId, cancellationToken);
        row.LeaseExpiresAtUtc = clock.UtcNow + leaseDuration; row.ConcurrencyToken = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task StartAsync(JudgeWorkerId workerId, JudgeJobId jobId, Guid leaseId, CancellationToken cancellationToken)
    {
        var row = await OwnedLeaseAsync(workerId, jobId, leaseId, cancellationToken);
        row.State = (int)JudgeJobState.Running; row.StartedAtUtc ??= clock.UtcNow; row.ConcurrencyToken = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(JudgeWorkerId workerId, JudgeJobId jobId, Guid leaseId, Judgement judgement, CancellationToken cancellationToken)
        => await CompleteDetailedAsync(workerId, jobId, leaseId, new(judgement, null, 0, null, null, null, 0, 0,
            null, null, null, "legacy", workerId, "legacy"), cancellationToken);

    public async Task CompleteDetailedAsync(JudgeWorkerId workerId, JudgeJobId jobId, Guid leaseId,
        WorkerJudgementCompletion completion, CancellationToken cancellationToken)
    {
        var judgement = completion.Judgement;
        ValidateCompletion(workerId, completion);
        var existing = await db.JudgeJobs.SingleOrDefaultAsync(x => x.Id == jobId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Job not found.");
        if (existing.State == (int)JudgeJobState.Completed && existing.WorkerId == workerId.Value) return;
        var row = await OwnedLeaseAsync(workerId, jobId, leaseId, cancellationToken);
        if (row.SubmissionId != judgement.SubmissionId.Value) throw LeaseFailure();
        row.State = (int)JudgeJobState.Completed; row.CompletedAtUtc = clock.UtcNow; row.LeaseExpiresAtUtc = null;
        row.ConcurrencyToken = Guid.NewGuid();
        if (!await db.Judgements.AnyAsync(x => x.SubmissionId == row.SubmissionId, cancellationToken))
            db.Judgements.Add(new JudgementRow
            {
                SubmissionId = row.SubmissionId,
                Verdict = (int)judgement.Verdict,
                Score = judgement.Score,
                CompletedAtUtc = clock.UtcNow,
                FailedTestIndex = completion.FailedTestIndex,
                ExecutionMilliseconds = completion.ExecutionMilliseconds,
                PeakMemoryBytes = completion.PeakMemoryBytes,
                ExitCode = completion.ExitCode,
                Signal = completion.Signal,
                StandardOutputBytes = completion.StandardOutputBytes,
                StandardErrorBytes = completion.StandardErrorBytes,
                CompilerDiagnosticSummary = completion.CompilerDiagnosticSummary,
                RuntimeDiagnosticSummary = completion.RuntimeDiagnosticSummary,
                CheckerDiagnosticSummary = completion.CheckerDiagnosticSummary,
                SandboxBackend = completion.SandboxBackend,
                WorkerId = workerId.Value,
                JudgeVersion = completion.JudgeVersion
            });
        AddOutbox(new JudgeJobCompleted(jobId, new SubmissionId(row.SubmissionId)));
        AddOutbox(new JudgementUpdated(new SubmissionId(row.SubmissionId), judgement.Verdict));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateCompletion(JudgeWorkerId workerId, WorkerJudgementCompletion completion)
    {
        if (completion.WorkerId != workerId || completion.ExecutionMilliseconds < 0 || completion.PeakMemoryBytes < 0 ||
            completion.StandardOutputBytes < 0 || completion.StandardErrorBytes < 0 || completion.FailedTestIndex < 0 ||
            string.IsNullOrWhiteSpace(completion.SandboxBackend) || completion.SandboxBackend.Length > 100 ||
            string.IsNullOrWhiteSpace(completion.JudgeVersion) || completion.JudgeVersion.Length > 100 ||
            completion.CompilerDiagnosticSummary?.Length > 4096 || completion.RuntimeDiagnosticSummary?.Length > 1024 ||
            completion.CheckerDiagnosticSummary?.Length > 1024)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Judgement result metadata is invalid.");
    }

    public async Task FailAsync(JudgeWorkerId workerId, JudgeJobId jobId, Guid leaseId, string failureCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(failureCode) || failureCode.Length > 100)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Failure code is invalid.");
        var row = await OwnedLeaseAsync(workerId, jobId, leaseId, cancellationToken);
        row.FailureCode = failureCode;
        if (row.Attempt >= row.MaximumAttempts) { row.State = (int)JudgeJobState.Failed; row.CompletedAtUtc = clock.UtcNow; }
        else { row.State = (int)JudgeJobState.Pending; row.AvailableAtUtc = clock.UtcNow.AddSeconds(Math.Pow(2, row.Attempt)); }
        row.WorkerId = null; row.LeaseId = null; row.LeaseExpiresAtUtc = null; row.ConcurrencyToken = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<JudgeJobRow> OwnedLeaseAsync(JudgeWorkerId workerId, JudgeJobId jobId, Guid leaseId, CancellationToken ct)
    {
        var row = await db.JudgeJobs.SingleOrDefaultAsync(x => x.Id == jobId.Value, ct)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Job not found.");
        if (row.WorkerId != workerId.Value || row.LeaseId != leaseId || row.LeaseExpiresAtUtc <= clock.UtcNow ||
            row.State is not ((int)JudgeJobState.Claimed) and not ((int)JudgeJobState.Running)) throw LeaseFailure();
        return row;
    }

    private void AddOutbox<T>(T message) where T : notnull => db.OutboxMessages.Add(new OutboxRow
    {
        Id = Guid.NewGuid(),
        Type = typeof(T).FullName ?? typeof(T).Name,
        Payload = JsonSerializer.Serialize(message),
        OccurredAtUtc = clock.UtcNow,
        CreatedAtUtc = clock.UtcNow,
        NextAttemptAtUtc = clock.UtcNow
    });
    private static void ValidateLease(TimeSpan lease) { if (lease < TimeSpan.FromSeconds(5) || lease > TimeSpan.FromMinutes(30)) throw new ApplicationFailure(ErrorCodes.InvalidInput, "Lease duration is invalid."); }
    private static ApplicationFailure LeaseFailure() => new(ErrorCodes.LeaseRejected, "The worker does not own an active lease for this job.");
}
