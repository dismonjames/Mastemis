using Mastemis.Application;
using Mastemis.Application.Problems.Assets;
using Mastemis.Contracts.Problems.ReferenceOutputs;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed record ReferenceOutputUpload(Guid OperationId, Guid LeaseToken, int ContractVersion, int TestIndex,
    string Sha256, long Length, long ExecutionMilliseconds, long? PeakMemoryBytes, string SandboxBackend,
    string JudgeVersion, ReferenceOutputResultStatus Status, string? FailureCode);

public sealed class ReferenceOutputIngestionService(MastemisDbContext db, IProblemObjectStorage objects, IClock clock)
{
    public async Task IngestAsync(JudgeWorkerId workerId, Guid jobId, ReferenceOutputUpload upload, Stream content,
        CancellationToken cancellationToken)
    {
        Validate(upload); var staged = await objects.StageAsync(ProblemObjectKind.ExpectedOutput, content, 67_108_864, cancellationToken);
        if (staged.Length != upload.Length || !string.Equals(staged.Sha256, upload.Sha256, StringComparison.Ordinal))
        { await objects.DeleteStagedAsync(staged.ObjectId, CancellationToken.None); throw new ApplicationFailure(ErrorCodes.InvalidInput, "Expected output hash or length does not match."); }
        var committed = false;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var job = await db.ReferenceOutputJobs.SingleOrDefaultAsync(x => x.Id == jobId, cancellationToken)
                ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Reference job not found.");
            if (job.OperationId != upload.OperationId || job.WorkerId != workerId.Value || job.LeaseToken != upload.LeaseToken ||
                job.LeaseExpiresAtUtc <= clock.UtcNow || job.Status is not ((int)ReferenceOutputJobStatus.Claimed) and not ((int)ReferenceOutputJobStatus.Running))
                throw new ApplicationFailure(ErrorCodes.LeaseRejected, "Worker does not own this reference output job.");
            var test = await db.GeneratedTests.Join(db.GeneratedTestSets.Where(x => x.GenerationOperationId == upload.OperationId && !x.Published),
                x => x.TestSetId, x => x.Id, (test, _) => test).SingleOrDefaultAsync(x => x.TestIndex == upload.TestIndex, cancellationToken)
                ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Reference output test does not belong to the operation.");
            if (test.OutputObjectId is not null)
            {
                if (test.OutputSha256 == staged.Sha256 && test.OutputLength == staged.Length)
                { await transaction.CommitAsync(cancellationToken); await objects.DeleteStagedAsync(staged.ObjectId, CancellationToken.None); return; }
                throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "A conflicting expected output already exists.");
            }
            test.OutputObjectId = staged.ObjectId; test.OutputSha256 = staged.Sha256; test.OutputLength = staged.Length;
            var operation = await db.ProblemGenerationOperations.SingleAsync(x => x.Id == upload.OperationId, cancellationToken);
            operation.ExpectedOutputCount++; operation.ProgressNumerator = operation.GeneratedInputCount + operation.ExpectedOutputCount;
            operation.UpdatedAtUtc = clock.UtcNow; operation.ConcurrencyToken = Guid.NewGuid();
            db.OutboxMessages.Add(ProblemOutbox.Create("ReferenceOutputTestCompleted", operation.ProblemId, clock.UtcNow,
                new
                {
                    problemId = operation.ProblemId,
                    operationId = operation.Id,
                    jobId,
                    testIndex = upload.TestIndex,
                    completed = operation.ExpectedOutputCount,
                    total = operation.GeneratedInputCount
                }));
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); committed = true;
        }
        catch { if (!committed) await objects.DeleteStagedAsync(staged.ObjectId, CancellationToken.None); throw; }
    }

    private static void Validate(ReferenceOutputUpload upload)
    {
        if (upload.ContractVersion != ReferenceOutputJobPayload.CurrentVersion || upload.OperationId == Guid.Empty || upload.LeaseToken == Guid.Empty ||
            upload.TestIndex < 1 || upload.Sha256.Length != 64 || upload.Length is < 0 or > 67_108_864 || upload.ExecutionMilliseconds < 0 ||
            upload.PeakMemoryBytes < 0 || string.IsNullOrWhiteSpace(upload.SandboxBackend) || upload.SandboxBackend.Length > 100 ||
            string.IsNullOrWhiteSpace(upload.JudgeVersion) || upload.JudgeVersion.Length > 100 || upload.FailureCode?.Length > 100 ||
            upload.Status != ReferenceOutputResultStatus.Completed)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Reference output result metadata is invalid.");
    }
}
