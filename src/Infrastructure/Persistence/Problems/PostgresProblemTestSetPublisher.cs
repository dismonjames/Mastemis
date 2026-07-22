using Mastemis.Application;
using Mastemis.Application.Problems.Assets;
using Mastemis.Application.Problems.Authoring;
using Mastemis.Application.Problems.TestSets;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class PostgresProblemTestSetPublisher(MastemisDbContext db, IProblemObjectStorage objects, IClock clock)
    : IProblemTestSetPublisher
{
    public async Task<Guid> PublishAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var promotionIds = new List<string>();
        Guid publishedId;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var operation = await db.ProblemGenerationOperations.SingleOrDefaultAsync(x => x.Id == operationId, cancellationToken)
                ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Generation operation not found.");
            if (operation.Status == (int)GenerationOperationStatus.Completed && operation.PublishedTestSetId is { } existing)
                return existing;
            if (operation.Status != (int)GenerationOperationStatus.WaitingForReferenceOutputs)
                throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "Generation operation is not ready to publish.");
            if (await IsAssignedToOpenExamAsync(operation.ProblemId, cancellationToken))
                throw new ApplicationFailure(ErrorCodes.Forbidden, "Tests attached to an open examination cannot be replaced.");
            var job = await db.ReferenceOutputJobs.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == operationId, cancellationToken);
            if (job is null || job.Status != (int)Contracts.Problems.ReferenceOutputs.ReferenceOutputJobStatus.Completed)
                throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "Reference output generation is incomplete.");
            var set = await db.GeneratedTestSets.SingleAsync(x => x.GenerationOperationId == operationId && !x.Published, cancellationToken);
            var tests = await db.GeneratedTests.Where(x => x.TestSetId == set.Id).OrderBy(x => x.TestIndex).ToArrayAsync(cancellationToken);
            ValidateComplete(operation, tests);
            operation.Status = (int)GenerationOperationStatus.Publishing;
            operation.UpdatedAtUtc = clock.UtcNow;
            operation.ConcurrencyToken = Guid.NewGuid();
            await db.ProblemTestCases.Where(x => x.ProblemId == operation.ProblemId).ExecuteDeleteAsync(cancellationToken);
            foreach (var test in tests)
            {
                db.ProblemTestCases.Add(new()
                {
                    Id = test.Id,
                    ProblemId = operation.ProblemId,
                    TestIndex = test.TestIndex,
                    InputObjectId = test.InputObjectId,
                    ExpectedObjectId = test.OutputObjectId!,
                    InputBytes = test.InputLength,
                    ExpectedBytes = test.OutputLength!.Value,
                    CheckerId = test.Checker
                });
                promotionIds.Add(test.InputObjectId);
                promotionIds.Add(test.OutputObjectId!);
            }
            var draft = await db.ProblemDrafts.AsNoTracking().SingleAsync(x => x.Id == operation.ProblemId, cancellationToken);
            var profile = await db.ProblemJudgeProfiles.SingleOrDefaultAsync(x => x.ProblemId == operation.ProblemId, cancellationToken);
            if (profile is null)
            {
                profile = new() { ProblemId = operation.ProblemId };
                db.ProblemJudgeProfiles.Add(profile);
            }
            profile.CpuMilliseconds = draft.TimeLimitMilliseconds;
            profile.WallMilliseconds = Math.Max(draft.TimeLimitMilliseconds * 2, draft.TimeLimitMilliseconds + 250);
            profile.MemoryBytes = draft.MemoryLimitBytes;
            profile.OutputBytes = draft.OutputLimitBytes;
            profile.FileBytes = Math.Max(draft.OutputLimitBytes, 67_108_864);
            profile.ProcessCount = 16;
            profile.TestCount = tests.Length;
            profile.CompilationMilliseconds = 30_000;
            profile.CompilationOutputBytes = 4_194_304;
            set.Published = true;
            set.PublishedAtUtc = clock.UtcNow;
            operation.Status = (int)GenerationOperationStatus.Completed;
            operation.CompletedAtUtc = clock.UtcNow;
            operation.UpdatedAtUtc = clock.UtcNow;
            operation.PublishedTestSetId = set.Id;
            operation.ProgressNumerator = operation.ProgressDenominator;
            operation.ConcurrencyToken = Guid.NewGuid();
            db.OutboxMessages.Add(ProblemOutbox.Create("GeneratedTestSetPublished", operation.ProblemId, clock.UtcNow,
                new { problemId = operation.ProblemId, operationId, testSetId = set.Id, testCount = tests.Length }));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "Test publication conflicted with another operation.");
            }
            publishedId = set.Id;
        }
        foreach (var objectId in promotionIds.Distinct(StringComparer.Ordinal))
            await objects.MarkReferencedAsync(objectId, cancellationToken);
        return publishedId;
    }

    private async Task<bool> IsAssignedToOpenExamAsync(Guid problemId, CancellationToken cancellationToken) =>
        await (from assignment in db.ExamProblemAssignments.AsNoTracking()
               join exam in db.Exams.AsNoTracking() on assignment.ExamId equals exam.Id
               where assignment.ProblemId == problemId && exam.State == (int)ExamState.Open
               select assignment).AnyAsync(cancellationToken);

    private static void ValidateComplete(ProblemGenerationOperationRow operation, GeneratedTestRow[] tests)
    {
        if (tests.Length == 0 || tests.Length != operation.GeneratedInputCount || operation.ExpectedOutputCount != tests.Length ||
            tests.Select(x => x.TestIndex).Distinct().Count() != tests.Length || tests.Select(x => x.TestIndex).Order().SequenceEqual(Enumerable.Range(1, tests.Length)) is false ||
            tests.Any(x => string.IsNullOrWhiteSpace(x.InputObjectId) || x.InputSha256.Length != 64 || x.InputLength < 0 ||
                string.IsNullOrWhiteSpace(x.OutputObjectId) || x.OutputSha256?.Length != 64 || x.OutputLength is null or < 0))
            throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "Generated tests are incomplete.");
    }
}
