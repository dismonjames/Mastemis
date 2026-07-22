using System.Text.Json;
using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Application.Problems.Assets;
using Mastemis.Application.Problems.Authoring;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class PostgresProblemStudioStore(MastemisDbContext db, IProblemObjectStorage objects, IClock clock,
    IAdministrationActor actor) : IProblemStudioStore
{
    public async Task<DraftProblem> CreateAsync(string title, string locale, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var row = new ProblemDraftRow { Id = Guid.NewGuid(), Title = title, DefaultLocale = locale, CreatedAtUtc = now, UpdatedAtUtc = now, ConcurrencyToken = Guid.NewGuid() };
        db.ProblemDrafts.Add(row);
        db.ProblemAuthorAssignments.Add(new()
        {
            ProblemId = row.Id,
            UserId = actor.UserId.Value,
            Role = 0,
            Status = 0,
            AssignedByUserId = actor.UserId.Value,
            AssignedAtUtc = now
        });
        AddEvent("ProblemDraftCreated", row.Id, new { problemId = row.Id });
        await SaveAsync(cancellationToken);
        return Map(row);
    }

    public async Task<DraftProblem?> GetAsync(ProblemId problemId, CancellationToken cancellationToken)
    {
        var row = await db.ProblemDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == problemId.Value, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task SaveMasAsync(ProblemId problemId, string source, string sha256, CancellationToken cancellationToken)
    {
        var row = await FindProblemAsync(problemId, cancellationToken);
        row.MasSource = source; row.MasSha256 = sha256; row.UpdatedAtUtc = clock.UtcNow; row.ConcurrencyToken = Guid.NewGuid();
        AddEvent("ProblemDraftUpdated", row.Id, new { problemId = row.Id, component = "mas" });
        await SaveAsync(cancellationToken);
    }

    public async Task<ProblemGenerationOperation> BeginGenerationAsync(ProblemId problemId, ulong seed, string runtimeVersion,
        CancellationToken cancellationToken)
    {
        _ = await FindProblemAsync(problemId, cancellationToken);
        var existing = await db.ProblemGenerationOperations.SingleOrDefaultAsync(x => x.ProblemId == problemId.Value && x.Status <= 1, cancellationToken);
        if (existing is not null) return Map(existing);
        var row = new ProblemGenerationOperationRow
        {
            Id = Guid.NewGuid(),
            ProblemId = problemId.Value,
            Status = (int)GenerationOperationStatus.Pending,
            Seed = seed,
            RuntimeVersion = runtimeVersion,
            CreatedAtUtc = clock.UtcNow,
            ConcurrencyToken = Guid.NewGuid()
        };
        db.ProblemGenerationOperations.Add(row); AddEvent("GenerationStarted", problemId.Value, new { problemId = problemId.Value, operationId = row.Id });
        await SaveAsync(cancellationToken);
        return Map(row);
    }

    public async Task PublishTestsAsync(ProblemGenerationOperation operation,
        IReadOnlyList<(int Index, string Group, byte[] Input, string Hash)> tests, CancellationToken cancellationToken)
    {
        if (tests.Count == 0 || tests.Select(x => x.Index).Distinct().Count() != tests.Count)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Generated tests are invalid.");
        var orderedTests = tests.OrderBy(x => x.Index).ToArray();
        var staged = new List<StagedProblemObject>(tests.Count);
        var committed = false;
        try
        {
            foreach (var test in orderedTests)
            {
                var item = await objects.StageAsync(ProblemObjectKind.TestInput, new MemoryStream(test.Input, false), 67_108_864, cancellationToken);
                if (!string.Equals(item.Sha256, test.Hash, StringComparison.Ordinal))
                    throw new ApplicationFailure(ErrorCodes.InvalidInput, "Generated test hash does not match.");
                staged.Add(item);
            }
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var row = await db.ProblemGenerationOperations.SingleOrDefaultAsync(x => x.Id == operation.Id, cancellationToken)
                ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Generation operation not found.");
            if (row.Status == (int)GenerationOperationStatus.Completed)
            {
                foreach (var item in staged) await objects.DeleteStagedAsync(item.ObjectId, cancellationToken);
                return;
            }
            if (row.Status is (int)GenerationOperationStatus.Failed or (int)GenerationOperationStatus.Cancelled)
                throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "Generation operation is terminal.");
            var version = (await db.GeneratedTestSets.Where(x => x.ProblemId == row.ProblemId).MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0) + 1;
            var set = new GeneratedTestSetRow { Id = Guid.NewGuid(), ProblemId = row.ProblemId, GenerationOperationId = row.Id, Version = version, Published = true, CreatedAtUtc = clock.UtcNow, PublishedAtUtc = clock.UtcNow };
            db.GeneratedTestSets.Add(set);
            for (var index = 0; index < tests.Count; index++)
            {
                var test = orderedTests[index]; var item = staged[index];
                db.GeneratedTests.Add(new() { Id = Guid.NewGuid(), TestSetId = set.Id, TestIndex = test.Index, Group = test.Group, InputObjectId = item.ObjectId, InputSha256 = item.Sha256, InputLength = item.Length });
            }
            row.Status = (int)GenerationOperationStatus.Completed; row.CompletedAtUtc = clock.UtcNow; row.PublishedTestSetId = set.Id; row.ConcurrencyToken = Guid.NewGuid();
            AddEvent("GeneratedTestSetPublished", row.ProblemId, new { problemId = row.ProblemId, operationId = row.Id, testSetId = set.Id, count = tests.Count });
            await SaveAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); committed = true;
            foreach (var item in staged) await objects.MarkReferencedAsync(item.ObjectId, cancellationToken);
        }
        catch
        {
            if (!committed)
                foreach (var item in staged) await objects.DeleteStagedAsync(item.ObjectId, CancellationToken.None);
            throw;
        }
    }

    public Task FailGenerationAsync(Guid operationId, string failureCode, CancellationToken cancellationToken) =>
        FinishAsync(operationId, GenerationOperationStatus.Failed, failureCode, cancellationToken);

    public Task CancelGenerationAsync(Guid operationId, CancellationToken cancellationToken) =>
        FinishAsync(operationId, GenerationOperationStatus.Cancelled, null, cancellationToken);

    public async Task<ProblemGenerationOperation?> GetGenerationAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var row = await db.ProblemGenerationOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == operationId, cancellationToken);
        return row is null ? null : Map(row);
    }

    private async Task FinishAsync(Guid id, GenerationOperationStatus status, string? failureCode, CancellationToken cancellationToken)
    {
        var row = await db.ProblemGenerationOperations.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Generation operation not found.");
        if (row.Status >= (int)GenerationOperationStatus.Completed) return;
        row.Status = (int)status; row.CompletedAtUtc = clock.UtcNow; row.FailureCode = failureCode; row.ConcurrencyToken = Guid.NewGuid();
        AddEvent(status == GenerationOperationStatus.Cancelled ? "GenerationCancelled" : "GenerationFailed", row.ProblemId,
            new { problemId = row.ProblemId, operationId = row.Id, failureCode });
        await SaveAsync(cancellationToken);
    }

    private async Task<ProblemDraftRow> FindProblemAsync(ProblemId id, CancellationToken cancellationToken) =>
        await db.ProblemDrafts.SingleOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
        ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem not found.");

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "Problem draft was changed concurrently."); }
    }

    private void AddEvent(string type, Guid resourceId, object payload) => db.OutboxMessages.Add(new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        ContractVersion = 1,
        ResourceId = resourceId.ToString("N"),
        Payload = JsonSerializer.Serialize(payload),
        OccurredAtUtc = clock.UtcNow,
        CreatedAtUtc = clock.UtcNow,
        NextAttemptAtUtc = clock.UtcNow
    });

    private static DraftProblem Map(ProblemDraftRow row) => new(new(row.Id), row.Title, row.DefaultLocale,
        new Dictionary<string, string>(), row.TimeLimitMilliseconds, row.MemoryLimitBytes, row.OutputLimitBytes, row.Checker, row.MasSource, row.MasSha256);
    private static ProblemGenerationOperation Map(ProblemGenerationOperationRow row) => new(row.Id, new(row.ProblemId),
        (GenerationOperationStatus)row.Status, row.Seed, row.RuntimeVersion, row.CreatedAtUtc, row.CompletedAtUtc, row.FailureCode);
}
