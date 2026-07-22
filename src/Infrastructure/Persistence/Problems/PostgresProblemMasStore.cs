using System.Text.Json;
using Mastemis.Application;
using Mastemis.Application.Problems.Mas;
using Mastemis.Domain;
using Mastemis.Mas.Language.Diagnostics;
using Mastemis.Mas.Runtime.Execution;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class PostgresProblemMasStore(MastemisDbContext db, IClock clock) : IProblemMasStore
{
    public async Task<ProblemMasSource?> GetAsync(ProblemId problemId, CancellationToken cancellationToken)
    {
        var row = await db.ProblemDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == problemId.Value, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<ProblemMasSource> SaveAsync(ProblemId problemId, string source, string sha256, int expectedRevision,
        IReadOnlyList<MasDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var row = await db.ProblemDrafts.SingleOrDefaultAsync(x => x.Id == problemId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem draft not found.");
        if (row.MasRevision != expectedRevision) throw Conflict();
        row.MasSource = source; row.MasSha256 = sha256; row.MasRevision++; row.MasValidationJson = JsonSerializer.Serialize(diagnostics);
        row.MasValidatedAtUtc = clock.UtcNow; row.MasRuntimeVersion = MasRuntime.RuntimeVersion;
        row.Version++; row.UpdatedAtUtc = clock.UtcNow; row.ConcurrencyToken = Guid.NewGuid();
        db.OutboxMessages.Add(ProblemOutbox.Create("MasSourceUpdated", row.Id, clock.UtcNow,
            new { problemId = row.Id, revision = row.MasRevision, sha256 }));
        try { await db.SaveChangesAsync(cancellationToken); } catch (DbUpdateConcurrencyException) { throw Conflict(); }
        return Map(row);
    }

    public async Task SaveValidationAsync(ProblemId problemId, string sha256, IReadOnlyList<MasDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var row = await db.ProblemDrafts.SingleOrDefaultAsync(x => x.Id == problemId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem draft not found.");
        row.MasValidationJson = JsonSerializer.Serialize(diagnostics); row.MasValidatedAtUtc = clock.UtcNow;
        db.OutboxMessages.Add(ProblemOutbox.Create("MasValidationCompleted", row.Id, clock.UtcNow,
            new { problemId = row.Id, sha256, valid = diagnostics.All(x => x.Severity != MasDiagnosticSeverity.Error) }));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static ProblemMasSource Map(ProblemDraftRow row) => new(new(row.Id), row.MasSource, row.MasSha256,
        row.MasRevision, row.MasRuntimeVersion, row.MasValidatedAtUtc,
        JsonSerializer.Deserialize<MasDiagnostic[]>(row.MasValidationJson) ?? []);
    private static ApplicationFailure Conflict() => new(ErrorCodes.IdempotencyConflict, "MAS source revision conflict.");
}
