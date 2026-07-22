using System.Text.Json;
using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Application.Problems.Drafts;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class PostgresProblemDraftService(MastemisDbContext db, IAuthorizationService authorization,
    IAdministrationActor actor, IClock clock) : IProblemDraftService
{
    public async Task<IReadOnlyList<ProblemDraftDetails>> ListAsync(CancellationToken cancellationToken)
    {
        var assigned = db.ProblemAuthorAssignments.AsNoTracking().Where(x => x.UserId == actor.UserId.Value && x.Status == 0 &&
            (x.ExpiresAtUtc == null || x.ExpiresAtUtc > clock.UtcNow)).Select(x => x.ProblemId);
        var managed = from problem in db.ExamProblemAssignments.AsNoTracking()
                      join assignment in db.ExamAssignments.AsNoTracking() on problem.ExamId equals assignment.ExamId
                      where assignment.UserId == actor.UserId.Value && assignment.Role == MastemisRoles.ExamManager
                      select problem.ProblemId;
        return (await db.ProblemDrafts.AsNoTracking().Where(x => assigned.Contains(x.Id) || managed.Contains(x.Id))
            .OrderByDescending(x => x.UpdatedAtUtc).Take(500).ToArrayAsync(cancellationToken)).Select(Map).ToArray();
    }

    public async Task<ProblemDraftDetails> GetAsync(ProblemId problemId, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("problem.read", problemId.Value, cancellationToken);
        var row = await db.ProblemDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == problemId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem draft not found.");
        return Map(row);
    }

    public async Task<ProblemDraftDetails> UpdateAsync(ProblemId problemId, ProblemDraftUpdate update, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("problem.manage", problemId.Value, cancellationToken); Validate(update);
        var row = await db.ProblemDrafts.SingleOrDefaultAsync(x => x.Id == problemId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem draft not found.");
        if (row.Version != update.ExpectedVersion) throw Conflict();
        if (!await db.ProblemStatements.AnyAsync(x => x.ProblemId == problemId.Value && x.Locale == update.DefaultLocale, cancellationToken))
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Default locale must have a localized statement.");
        row.Title = update.Title.Trim(); row.AuthorsJson = JsonSerializer.Serialize(update.Authors.Select(x => x.Trim()).ToArray());
        row.TagsJson = JsonSerializer.Serialize(update.Tags.Select(x => x.Trim()).ToArray()); row.Difficulty = update.Difficulty.Trim().ToLowerInvariant();
        row.DefaultLocale = update.DefaultLocale.Trim().ToLowerInvariant(); row.AcceptedLanguagesJson = JsonSerializer.Serialize(update.AcceptedLanguages);
        row.TimeLimitMilliseconds = update.TimeLimitMilliseconds; row.MemoryLimitBytes = update.MemoryLimitBytes;
        row.OutputLimitBytes = update.OutputLimitBytes; row.Checker = update.Checker; row.Version++;
        row.UpdatedAtUtc = clock.UtcNow; row.ConcurrencyToken = Guid.NewGuid();
        db.OutboxMessages.Add(ProblemOutbox.Create("ProblemDraftUpdated", row.Id, clock.UtcNow,
            new { problemId = row.Id, version = row.Version }));
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw Conflict(); }
        return Map(row);
    }

    public async Task DeleteAsync(ProblemId problemId, int expectedVersion, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("problem.manage", problemId.Value, cancellationToken);
        var row = await db.ProblemDrafts.SingleOrDefaultAsync(x => x.Id == problemId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem draft not found.");
        if (row.Version != expectedVersion) throw Conflict();
        if (await db.ExamProblemAssignments.AnyAsync(x => x.ProblemId == row.Id, cancellationToken) ||
            await db.GeneratedTestSets.AnyAsync(x => x.ProblemId == row.Id, cancellationToken) ||
            await db.ProblemPackageImports.AnyAsync(x => x.ProblemId == row.Id, cancellationToken))
            throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "Problem draft has durable history and cannot be deleted.");
        db.ProblemDrafts.Remove(row); await db.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(ProblemDraftUpdate value)
    {
        var languages = value.AcceptedLanguages.Distinct(StringComparer.Ordinal).ToArray();
        if (value.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(value.Title) || value.Title.Length > 300 ||
            value.Authors.Count > 32 || value.Authors.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 100) ||
            value.Tags.Count > 64 || value.Tags.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 50) ||
            value.Difficulty.Length is < 1 or > 32 || value.DefaultLocale.Length is < 2 or > 16 ||
            languages.Length == 0 || languages.Any(x => x is not ("cpp" or "csharp")) ||
            value.TimeLimitMilliseconds is < 50 or > 600_000 || value.MemoryLimitBytes is < 16_777_216 or > 17_179_869_184 ||
            value.OutputLimitBytes is < 1 or > 67_108_864 || value.Checker is not ("exact" or "tokens"))
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Problem draft metadata is invalid.");
    }
    private static ProblemDraftDetails Map(ProblemDraftRow row) => new(new(row.Id), row.Title,
        JsonSerializer.Deserialize<string[]>(row.AuthorsJson) ?? [], JsonSerializer.Deserialize<string[]>(row.TagsJson) ?? [],
        row.Difficulty, row.DefaultLocale, JsonSerializer.Deserialize<string[]>(row.AcceptedLanguagesJson) ?? [],
        row.TimeLimitMilliseconds, row.MemoryLimitBytes, row.OutputLimitBytes, row.Checker, row.Version, row.UpdatedAtUtc);
    private static ApplicationFailure Conflict() => new(ErrorCodes.IdempotencyConflict, "Problem draft version conflict.");
}
