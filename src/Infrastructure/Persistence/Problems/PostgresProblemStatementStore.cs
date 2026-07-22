using System.Text.Json;
using System.Text.RegularExpressions;
using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Application.Problems.Assets;
using Mastemis.Application.Problems.Statements;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed partial class PostgresProblemStatementStore(MastemisDbContext db, IProblemObjectStorage objects,
    IAdministrationActor actor, IClock clock) : IProblemStatementStore
{
    public async Task<IReadOnlyList<ProblemStatementSummary>> ListAsync(ProblemId problemId, CancellationToken cancellationToken) =>
        await db.ProblemStatements.AsNoTracking().Where(x => x.ProblemId == problemId.Value).OrderBy(x => x.Locale)
            .Select(x => new ProblemStatementSummary(x.Locale, x.Title, x.Revision, x.Sha256, x.Length, x.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);

    public async Task<ProblemStatement?> GetAsync(ProblemId problemId, string locale, CancellationToken cancellationToken)
    {
        locale = NormalizeLocale(locale);
        var row = await db.ProblemStatements.AsNoTracking().SingleOrDefaultAsync(x => x.ProblemId == problemId.Value && x.Locale == locale, cancellationToken);
        if (row is null) return null;
        await using var stream = await objects.OpenReadAsync(row.ObjectId, 1_500_000, cancellationToken);
        var content = await JsonSerializer.DeserializeAsync<ProblemStatementContent>(stream, cancellationToken: cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.InvalidInput, "Stored problem statement is invalid.");
        return Map(row, content);
    }

    public async Task<ProblemStatement> SaveAsync(ProblemId problemId, string locale, ProblemStatementContent content,
        int? expectedRevision, CancellationToken cancellationToken)
    {
        locale = NormalizeLocale(locale);
        _ = await db.ProblemDrafts.SingleOrDefaultAsync(x => x.Id == problemId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem not found.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(content);
        var staged = await objects.StageAsync(ProblemObjectKind.Statement, new MemoryStream(bytes, false), 1_500_000, cancellationToken);
        var committed = false;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var row = await db.ProblemStatements.SingleOrDefaultAsync(x => x.ProblemId == problemId.Value && x.Locale == locale, cancellationToken);
            if (row is null)
            {
                if (expectedRevision is not null) throw Conflict();
                row = new() { ProblemId = problemId.Value, Locale = locale, Revision = 1 }; db.ProblemStatements.Add(row);
            }
            else
            {
                if (expectedRevision != row.Revision) throw Conflict();
                row.Revision++;
            }
            row.Title = content.Title.Trim(); row.ObjectId = staged.ObjectId; row.Sha256 = staged.Sha256; row.Length = staged.Length;
            row.UpdatedByUserId = actor.UserId.Value; row.UpdatedAtUtc = clock.UtcNow;
            db.OutboxMessages.Add(ProblemOutbox.Create("ProblemStatementUpdated", problemId.Value, clock.UtcNow,
                new { problemId = problemId.Value, locale, revision = row.Revision }));
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); committed = true;
            await objects.MarkReferencedAsync(staged.ObjectId, cancellationToken); return Map(row, content);
        }
        catch { if (!committed) await objects.DeleteStagedAsync(staged.ObjectId, CancellationToken.None); throw; }
    }

    public async Task DeleteAsync(ProblemId problemId, string locale, CancellationToken cancellationToken)
    {
        locale = NormalizeLocale(locale);
        var problem = await db.ProblemDrafts.SingleOrDefaultAsync(x => x.Id == problemId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem not found.");
        if (locale == problem.DefaultLocale) throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "The default statement cannot be deleted.");
        var row = await db.ProblemStatements.SingleOrDefaultAsync(x => x.ProblemId == problemId.Value && x.Locale == locale, cancellationToken);
        if (row is null) return; db.ProblemStatements.Remove(row); await db.SaveChangesAsync(cancellationToken);
    }

    private static ProblemStatement Map(ProblemStatementRow row, ProblemStatementContent content) =>
        new(new(row.ProblemId), row.Locale, content, row.Revision, row.Sha256, row.Length, new(row.UpdatedByUserId), row.UpdatedAtUtc);
    private static string NormalizeLocale(string locale)
    { locale = locale.Trim().ToLowerInvariant(); if (!LocalePattern().IsMatch(locale)) throw new ApplicationFailure(ErrorCodes.InvalidInput, "Locale is invalid."); return locale; }
    private static ApplicationFailure Conflict() => new(ErrorCodes.IdempotencyConflict, "Statement revision conflict.");
    [GeneratedRegex("^[a-z]{2,3}(?:-[a-z0-9]{2,8})?$")]
    private static partial Regex LocalePattern();
}
