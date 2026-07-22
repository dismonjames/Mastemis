using System.Text;
using System.Text.Json;
using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Application.Problems.Assets;
using Mastemis.Application.Problems.Packages;
using Mastemis.Application.Problems.Statements;
using Mastemis.Domain;
using Mastemis.Mas.Packaging.Archives;
using Mastemis.Mas.Packaging.Importing;
using Mastemis.Mas.Packaging.Security;
using Mastemis.Mas.Packaging.Validation;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class PostgresProblemPackageReplacer(MastemisDbContext db, IProblemObjectStorage objects,
    IAuthorizationService authorization, IAdministrationActor actor, IClock clock)
{
    public async Task<ProblemPackageImport> ReplaceAsync(Guid problemId, int expectedVersion, Stream package,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("problem.manage", problemId, cancellationToken);
        ValidateRequest(expectedVersion, idempotencyKey);
        var previous = await db.ProblemPackageImports.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (previous is not null)
        {
            if (previous.ProblemId != problemId || previous.Mode != "ReplaceDraft") throw Conflict();
            return Map(previous);
        }
        var document = (await new ProblemPackageImporter(new PackageArchiveReader(new()), new ProblemPackageValidator())
            .InspectAsync(package, cancellationToken)).Package;
        if (document.Manifest.ProblemId != problemId) throw new ApplicationFailure(ErrorCodes.InvalidInput, "Replacement package identity must match the target problem.");
        var staged = new Dictionary<string, StagedProblemObject>(StringComparer.Ordinal);
        var committed = false;
        try
        {
            foreach (var path in ReferencedPaths(document))
                staged[path] = await objects.StageAsync(Kind(path), new MemoryStream(document.Entries[path], false), Maximum(Kind(path)), cancellationToken);
            foreach (var statement in document.Manifest.Statements)
            {
                var value = new ProblemStatementContent(document.Manifest.Title, Encoding.UTF8.GetString(document.Entries[statement.Value]),
                    string.Empty, string.Empty, string.Empty, string.Empty);
                var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
                staged[$"statement:{statement.Key}"] = await objects.StageAsync(ProblemObjectKind.Statement,
                    new MemoryStream(bytes, false), 1_500_000, cancellationToken);
            }
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var draft = await db.ProblemDrafts.SingleOrDefaultAsync(x => x.Id == problemId, cancellationToken)
                ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem draft not found.");
            if (draft.Version != expectedVersion) throw Conflict();
            if (await IsOpenExamProblemAsync(problemId, cancellationToken))
                throw new ApplicationFailure(ErrorCodes.Forbidden, "A problem attached to an open examination cannot be replaced.");
            var now = clock.UtcNow;
            draft.Title = document.Manifest.Title;
            draft.DefaultLocale = document.Manifest.DefaultLocale;
            draft.TimeLimitMilliseconds = document.Manifest.Limits.TimeMilliseconds;
            draft.MemoryLimitBytes = document.Manifest.Limits.MemoryBytes;
            draft.OutputLimitBytes = document.Manifest.Limits.OutputBytes;
            draft.Checker = document.Manifest.Checker.Type;
            draft.MasSource = ReadMas(document);
            draft.MasSha256 = string.IsNullOrEmpty(draft.MasSource) ? string.Empty : Sha256(Encoding.UTF8.GetBytes(draft.MasSource));
            draft.Version++;
            draft.UpdatedAtUtc = now;
            draft.ConcurrencyToken = Guid.NewGuid();
            await ReplaceStatementsAsync(problemId, document, staged, now, cancellationToken);
            await ReplaceAssetsAsync(problemId, document, staged, now, cancellationToken);
            await ReplaceReferenceSolutionAsync(problemId, document, staged, now, cancellationToken);
            await ReplaceTestsAsync(problemId, document, staged, now, cancellationToken);
            var import = new ProblemPackageImportRow
            {
                Id = Guid.NewGuid(),
                ProblemId = problemId,
                PackageSha256 = document.PackageSha256,
                IdempotencyKey = idempotencyKey,
                Mode = "ReplaceDraft",
                CreatedAtUtc = now
            };
            db.ProblemPackageImports.Add(import);
            db.OutboxMessages.Add(ProblemOutbox.Create("PackageImported", problemId, now,
                new { problemId, importId = import.Id, mode = import.Mode, version = draft.Version }));
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException) { throw Conflict(); }
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            foreach (var item in staged.Values.DistinctBy(x => x.ObjectId)) await objects.MarkReferencedAsync(item.ObjectId, cancellationToken);
            return Map(import);
        }
        catch
        {
            if (!committed) foreach (var item in staged.Values.DistinctBy(x => x.ObjectId))
                await objects.DeleteStagedAsync(item.ObjectId, CancellationToken.None);
            throw;
        }
    }

    private async Task ReplaceStatementsAsync(Guid problemId, ProblemPackageDocument document,
        IReadOnlyDictionary<string, StagedProblemObject> staged, DateTimeOffset now, CancellationToken ct)
    {
        await db.ProblemStatements.Where(x => x.ProblemId == problemId).ExecuteDeleteAsync(ct);
        foreach (var statement in document.Manifest.Statements)
        {
            var item = staged[$"statement:{statement.Key}"];
            db.ProblemStatements.Add(new()
            {
                ProblemId = problemId,
                Locale = statement.Key,
                Title = document.Manifest.Title,
                ObjectId = item.ObjectId,
                Sha256 = item.Sha256,
                Length = item.Length,
                Revision = 1,
                UpdatedByUserId = actor.UserId.Value,
                UpdatedAtUtc = now
            });
        }
    }

    private async Task ReplaceAssetsAsync(Guid problemId, ProblemPackageDocument document,
        IReadOnlyDictionary<string, StagedProblemObject> staged, DateTimeOffset now, CancellationToken ct)
    {
        await db.ProblemAssets.Where(x => x.ProblemId == problemId).ExecuteDeleteAsync(ct);
        foreach (var path in document.Manifest.Assets)
        {
            var item = staged[path]; var name = Path.GetFileName(path);
            db.ProblemAssets.Add(new()
            {
                Id = Guid.NewGuid(),
                ProblemId = problemId,
                LogicalName = name,
                NormalizedName = name.ToUpperInvariant(),
                ContentType = ContentType(name),
                ObjectId = item.ObjectId,
                Sha256 = item.Sha256,
                Length = item.Length,
                CreatedByUserId = actor.UserId.Value,
                CreatedAtUtc = now
            });
        }
    }

    private async Task ReplaceReferenceSolutionAsync(Guid problemId, ProblemPackageDocument document,
        IReadOnlyDictionary<string, StagedProblemObject> staged, DateTimeOffset now, CancellationToken ct)
    {
        var current = await db.ReferenceSolutionRevisions.Where(x => x.ProblemId == problemId && x.IsCurrent).ToArrayAsync(ct);
        foreach (var item in current) item.IsCurrent = false;
        if (document.Manifest.ReferenceSolutions.Count == 0) return;
        var language = document.Manifest.ReferenceSolutions[0].Language;
        if (document.Manifest.ReferenceSolutions.Any(x => x.Language != language)) throw Invalid();
        var revision = new ReferenceSolutionRevisionRow
        {
            Id = Guid.NewGuid(),
            ProblemId = problemId,
            Language = language,
            CompileProfile = language == "cpp" ? "cpp23-o2-v1" : "dotnet-no-restore-v1",
            CreatedByUserId = actor.UserId.Value,
            CreatedAtUtc = now,
            IsCurrent = true,
            Enabled = true
        };
        db.ReferenceSolutionRevisions.Add(revision);
        foreach (var source in document.Manifest.ReferenceSolutions)
        { var item = staged[source.Path]; db.ReferenceSolutionSources.Add(new() { RevisionId = revision.Id, FileName = Path.GetFileName(source.Path), ObjectId = item.ObjectId, Sha256 = item.Sha256, Length = item.Length }); }
    }

    private async Task ReplaceTestsAsync(Guid problemId, ProblemPackageDocument document,
        IReadOnlyDictionary<string, StagedProblemObject> staged, DateTimeOffset now, CancellationToken ct)
    {
        await db.ProblemTestCases.Where(x => x.ProblemId == problemId).ExecuteDeleteAsync(ct);
        var prior = await db.GeneratedTestSets.Where(x => x.ProblemId == problemId && x.Published).ToArrayAsync(ct);
        foreach (var set in prior) set.Published = false;
        if (document.Manifest.Tests.Count == 0) { await db.ProblemJudgeProfiles.Where(x => x.ProblemId == problemId).ExecuteDeleteAsync(ct); return; }
        var version = (await db.GeneratedTestSets.Where(x => x.ProblemId == problemId).MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
        var replacement = new GeneratedTestSetRow
        {
            Id = Guid.NewGuid(),
            ProblemId = problemId,
            GenerationOperationId = Guid.NewGuid(),
            Version = version,
            Published = true,
            CreatedAtUtc = now,
            PublishedAtUtc = now
        };
        db.GeneratedTestSets.Add(replacement);
        foreach (var test in document.Manifest.Tests.OrderBy(x => x.Index))
        {
            var input = staged[test.InputPath]; var output = staged[test.OutputPath!]; var id = Guid.NewGuid();
            db.GeneratedTests.Add(new()
            {
                Id = id,
                TestSetId = replacement.Id,
                TestIndex = test.Index,
                Group = test.GroupId,
                Visibility = document.Manifest.Groups.Single(x => x.Id == test.GroupId).Visibility,
                Checker = document.Manifest.Checker.Type,
                InputObjectId = input.ObjectId,
                InputSha256 = input.Sha256,
                InputLength = input.Length,
                OutputObjectId = output.ObjectId,
                OutputSha256 = output.Sha256,
                OutputLength = output.Length
            });
            db.ProblemTestCases.Add(new()
            {
                Id = id,
                ProblemId = problemId,
                TestIndex = test.Index,
                InputObjectId = input.ObjectId,
                ExpectedObjectId = output.ObjectId,
                InputBytes = input.Length,
                ExpectedBytes = output.Length,
                CheckerId = document.Manifest.Checker.Type
            });
        }
        var profile = await db.ProblemJudgeProfiles.SingleOrDefaultAsync(x => x.ProblemId == problemId, ct);
        profile ??= new ProblemJudgeProfileRow { ProblemId = problemId };
        if (db.Entry(profile).State == EntityState.Detached) db.ProblemJudgeProfiles.Add(profile);
        profile.CpuMilliseconds = document.Manifest.Limits.TimeMilliseconds;
        profile.WallMilliseconds = Math.Min(600_000, Math.Max(profile.CpuMilliseconds * 2, profile.CpuMilliseconds + 250));
        profile.MemoryBytes = document.Manifest.Limits.MemoryBytes; profile.OutputBytes = document.Manifest.Limits.OutputBytes;
        profile.FileBytes = Math.Max(profile.OutputBytes, 67_108_864); profile.ProcessCount = 16;
        profile.TestCount = document.Manifest.Tests.Count; profile.CompilationMilliseconds = 30_000; profile.CompilationOutputBytes = 4_194_304;
    }

    private Task<bool> IsOpenExamProblemAsync(Guid problemId, CancellationToken ct) =>
        (from assignment in db.ExamProblemAssignments.AsNoTracking()
         join exam in db.Exams.AsNoTracking()
         on assignment.ExamId equals exam.Id
         where assignment.ProblemId == problemId && exam.State == (int)ExamState.Open
         select assignment).AnyAsync(ct);
    private static void ValidateRequest(int version, string key) { if (version < 1 || string.IsNullOrWhiteSpace(key) || key.Length > 128) throw Invalid(); }
    private static IEnumerable<string> ReferencedPaths(ProblemPackageDocument document) => document.Manifest.Assets
        .Concat(document.Manifest.ReferenceSolutions.Select(x => x.Path)).Concat(document.Manifest.Tests.SelectMany(x => new[] { x.InputPath, x.OutputPath! })).Distinct(StringComparer.Ordinal);
    private static string ReadMas(ProblemPackageDocument document) => document.Manifest.Generators.FirstOrDefault(x => x.Language == "mas") is { } item ? Encoding.UTF8.GetString(document.Entries[item.Path]) : string.Empty;
    private static ProblemObjectKind Kind(string path) => path.StartsWith("assets/", StringComparison.Ordinal) ? ProblemObjectKind.Asset : path.StartsWith("solutions/", StringComparison.Ordinal) ? ProblemObjectKind.ReferenceSource : path.EndsWith(".out", StringComparison.Ordinal) ? ProblemObjectKind.ExpectedOutput : ProblemObjectKind.TestInput;
    private static long Maximum(ProblemObjectKind kind) => kind is ProblemObjectKind.Asset or ProblemObjectKind.ReferenceSource ? 4_194_304 : 67_108_864;
    private static string ContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp", _ => "text/plain" };
    private static string Sha256(byte[] value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(value)).ToLowerInvariant();
    private static ProblemPackageImport Map(ProblemPackageImportRow row) => new(row.Id, row.ProblemId, row.PackageSha256, row.Mode);
    private static ApplicationFailure Conflict() => new(ErrorCodes.IdempotencyConflict, "Problem draft version or package retry conflicts.");
    private static ApplicationFailure Invalid() => new(ErrorCodes.InvalidInput, "Replacement package request is invalid.");
}
