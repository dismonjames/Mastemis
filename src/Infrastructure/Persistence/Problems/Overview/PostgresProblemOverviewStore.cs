using System.Text.Json;
using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Application.Problems.Authorization;
using Mastemis.Application.Problems.Authoring;
using Mastemis.Application.Problems.Overview;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems.Overview;

public sealed class PostgresProblemOverviewStore(MastemisDbContext db, IAdministrationActor actor, IClock clock) : IProblemOverviewStore
{
    public async Task<ProblemOverview?> GetAsync(Guid id, CancellationToken ct)
    {
        var draft = await db.ProblemDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (draft is null) return null;
        var permission = await db.ProblemAuthorAssignments.AsNoTracking().Where(x => x.ProblemId == id && x.UserId == actor.UserId.Value && x.Status == 0 && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > clock.UtcNow))
            .Select(x => ((ProblemAuthorRole)x.Role).ToString()).FirstOrDefaultAsync(ct) ?? "ExaminationManager";
        var locked = await (from link in db.ExamProblemAssignments.AsNoTracking() join exam in db.Exams.AsNoTracking() on link.ExamId equals exam.Id where link.ProblemId == id && exam.State == (int)ExamState.Open select link).AnyAsync(ct);
        var reference = await db.ReferenceSolutionRevisions.AsNoTracking().Where(x => x.ProblemId == id && x.IsCurrent).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        var generation = await db.ProblemGenerationOperations.AsNoTracking().Where(x => x.ProblemId == id && x.Status != (int)GenerationOperationStatus.Completed && x.Status != (int)GenerationOperationStatus.Failed && x.Status != (int)GenerationOperationStatus.Cancelled).OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.Status }).FirstOrDefaultAsync(ct);
        var set = await db.GeneratedTestSets.AsNoTracking().Where(x => x.ProblemId == id && x.Published).OrderByDescending(x => x.Version).Select(x => new { x.Id, x.Version }).FirstOrDefaultAsync(ct);
        var groupCount = set is null ? 0 : await db.GeneratedTests.Where(x => x.TestSetId == set.Id).Select(x => x.Group).Distinct().CountAsync(ct);
        var testCount = set is null ? 0 : await db.GeneratedTests.CountAsync(x => x.TestSetId == set.Id, ct);
        var hidden = set is null ? 0 : await db.GeneratedTests.CountAsync(x => x.TestSetId == set.Id && x.Visibility == "hidden", ct);
        return new(id, draft.Title, draft.Version, permission, locked, await db.ProblemStatements.CountAsync(x => x.ProblemId == id, ct),
            JsonSerializer.Deserialize<string[]>(draft.AcceptedLanguagesJson) ?? [], draft.TimeLimitMilliseconds, draft.MemoryLimitBytes,
            draft.OutputLimitBytes, draft.Checker, draft.MasValidatedAtUtc is null ? "NotValidated" : draft.MasValidationJson == "[]" ? "Succeeded" : "Failed",
            draft.MasRuntimeVersion, reference, reference is null ? "NotConfigured" : "NotValidated", generation?.Id,
            generation is null ? null : ((GenerationOperationStatus)generation.Status).ToString(), set?.Version, groupCount, testCount, hidden,
            await db.ProblemPackageImports.Where(x => x.ProblemId == id).MaxAsync(x => (DateTimeOffset?)x.CreatedAtUtc, ct),
            await db.ProblemPackageExports.Where(x => x.ProblemId == id).MaxAsync(x => (DateTimeOffset?)x.CreatedAtUtc, ct));
    }
}
