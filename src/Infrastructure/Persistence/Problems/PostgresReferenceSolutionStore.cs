using System.Text.RegularExpressions;
using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Application.Problems.Assets;
using Mastemis.Application.Problems.ReferenceOutputs;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed partial class PostgresReferenceSolutionStore(MastemisDbContext db, IProblemObjectStorage objects,
    IAdministrationActor actor, IClock clock) : IReferenceSolutionStore
{
    public async Task<ReferenceSolutionRevision> SaveAsync(ProblemId problemId, string language,
        IReadOnlyList<ReferenceSolutionSourceInput> sources, CancellationToken cancellationToken)
    {
        language = language.Trim().ToLowerInvariant(); if (language is not ("cpp" or "csharp") || sources.Count is < 1 or > 32) throw Invalid();
        var extension = language == "cpp" ? ".cpp" : ".cs"; var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
        foreach (var source in sources) { if (!FilePattern().IsMatch(source.FileName) || !source.FileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase) || !names.Add(source.FileName)) throw Invalid(); total += source.Content.Length; }
        if (total is < 1 or > 4_194_304) throw Invalid();
        _ = await db.ProblemDrafts.SingleOrDefaultAsync(x => x.Id == problemId.Value, cancellationToken) ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem not found.");
        var staged = new List<(ReferenceSolutionSourceInput Source, StagedProblemObject Object)>(); var committed = false;
        try
        {
            foreach (var source in sources.OrderBy(x => x.FileName, StringComparer.Ordinal))
                staged.Add((source, await objects.StageAsync(ProblemObjectKind.ReferenceSource, new MemoryStream(source.Content.ToArray(), false), 4_194_304, cancellationToken)));
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var currents = await db.ReferenceSolutionRevisions.Where(x => x.ProblemId == problemId.Value && x.IsCurrent).ToListAsync(cancellationToken);
            foreach (var current in currents) current.IsCurrent = false;
            var revision = new ReferenceSolutionRevisionRow
            {
                Id = Guid.NewGuid(),
                ProblemId = problemId.Value,
                Language = language,
                CompileProfile = language == "cpp" ? "cpp23-o2-v1" : "dotnet-no-restore-v1",
                CreatedByUserId = actor.UserId.Value,
                CreatedAtUtc = clock.UtcNow,
                IsCurrent = true,
                Enabled = true
            };
            db.ReferenceSolutionRevisions.Add(revision);
            foreach (var item in staged) db.ReferenceSolutionSources.Add(new()
            {
                RevisionId = revision.Id,
                FileName = item.Source.FileName,
                ObjectId = item.Object.ObjectId,
                Sha256 = item.Object.Sha256,
                Length = item.Object.Length
            });
            db.OutboxMessages.Add(ProblemOutbox.Create("ProblemDraftUpdated", problemId.Value, clock.UtcNow,
                new { problemId = problemId.Value, component = "reference-solution", revisionId = revision.Id }));
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); committed = true;
            foreach (var item in staged) await objects.MarkReferencedAsync(item.Object.ObjectId, cancellationToken);
            return await GetCurrentAsync(problemId, cancellationToken) ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Reference solution was not persisted.");
        }
        catch { if (!committed) foreach (var item in staged) await objects.DeleteStagedAsync(item.Object.ObjectId, CancellationToken.None); throw; }
    }

    public async Task<ReferenceSolutionRevision?> GetCurrentAsync(ProblemId problemId, CancellationToken cancellationToken)
    {
        var row = await db.ReferenceSolutionRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.ProblemId == problemId.Value && x.IsCurrent, cancellationToken);
        if (row is null) return null; var sources = await db.ReferenceSolutionSources.AsNoTracking().Where(x => x.RevisionId == row.Id).OrderBy(x => x.FileName)
            .Select(x => new ReferenceSolutionSourceMetadata(x.FileName, x.Sha256, x.Length)).ToArrayAsync(cancellationToken);
        return new(row.Id, new(row.ProblemId), row.Language, sources, new(row.CreatedByUserId), row.CreatedAtUtc, row.Enabled);
    }

    public async Task<ReferenceSolutionSourceContent?> OpenSourceAsync(ProblemId problemId, Guid revisionId, string fileName, CancellationToken cancellationToken)
    {
        var row = await (from source in db.ReferenceSolutionSources.AsNoTracking()
                         join revision in db.ReferenceSolutionRevisions.AsNoTracking()
            on source.RevisionId equals revision.Id
                         where revision.ProblemId == problemId.Value && revision.Id == revisionId && source.FileName == fileName
                         select source).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : new(row.FileName, row.Sha256, await objects.OpenReadAsync(row.ObjectId, 4_194_304, cancellationToken));
    }
    private static ApplicationFailure Invalid() => new(ErrorCodes.InvalidInput, "Reference solution source is invalid.");
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$")]
    private static partial Regex FilePattern();
}
