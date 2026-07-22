using System.Text;
using System.Text.Json;
using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Application.Problems.Assets;
using Mastemis.Application.Problems.Packages;
using Mastemis.Application.Problems.Statements;
using Mastemis.Mas.Packaging.Archives;
using Mastemis.Mas.Packaging.Importing;
using Mastemis.Mas.Packaging.Security;
using Mastemis.Mas.Packaging.Validation;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class PostgresProblemPackageImporter(MastemisDbContext db, IProblemObjectStorage objects,
    IAuthorizationService authorization, IAdministrationActor actor, IClock clock)
{
    public async Task<ProblemPackageImport> CreateNewAsync(Stream package, string idempotencyKey, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("problem.create", Guid.Empty, cancellationToken);
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Package import idempotency key is invalid.");
        var existing = await db.ProblemPackageImports.AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null) return Map(existing);
        var inspected = await new ProblemPackageImporter(new PackageArchiveReader(new()), new ProblemPackageValidator()).InspectAsync(package, cancellationToken);
        var document = inspected.Package; var manifest = document.Manifest;
        if (await db.ProblemDrafts.AsNoTracking().AnyAsync(x => x.Id == manifest.ProblemId, cancellationToken))
            throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "Problem identity already exists.");
        var staged = new Dictionary<string, StagedProblemObject>(StringComparer.Ordinal); var committed = false;
        try
        {
            foreach (var path in ReferencedPaths(document))
            {
                var kind = Kind(path); var content = document.Entries[path];
                staged[path] = await objects.StageAsync(kind, new MemoryStream(content, false), Maximum(kind), cancellationToken);
            }
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken); var now = clock.UtcNow;
            db.ProblemDrafts.Add(new()
            {
                Id = manifest.ProblemId,
                Title = manifest.Title,
                DefaultLocale = manifest.DefaultLocale,
                TimeLimitMilliseconds = manifest.Limits.TimeMilliseconds,
                MemoryLimitBytes = manifest.Limits.MemoryBytes,
                OutputLimitBytes = manifest.Limits.OutputBytes,
                Checker = manifest.Checker.Type,
                MasSource = ReadMas(document),
                MasSha256 = ReadMas(document) is { Length: > 0 } source ? Sha256(Encoding.UTF8.GetBytes(source)) : string.Empty,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                ConcurrencyToken = Guid.NewGuid(),
                Version = 1
            });
            db.ProblemAuthorAssignments.Add(new() { ProblemId = manifest.ProblemId, UserId = actor.UserId.Value, Role = 0, Status = 0, AssignedByUserId = actor.UserId.Value, AssignedAtUtc = now });
            foreach (var statement in manifest.Statements)
            {
                var structured = new ProblemStatementContent(manifest.Title, Encoding.UTF8.GetString(document.Entries[statement.Value]), string.Empty, string.Empty, string.Empty, string.Empty);
                var bytes = JsonSerializer.SerializeToUtf8Bytes(structured); var item = await objects.StageAsync(ProblemObjectKind.Statement, new MemoryStream(bytes, false), 1_500_000, cancellationToken);
                staged[$"statement-record:{statement.Key}"] = item;
                db.ProblemStatements.Add(new() { ProblemId = manifest.ProblemId, Locale = statement.Key, Title = manifest.Title, ObjectId = item.ObjectId, Sha256 = item.Sha256, Length = item.Length, Revision = 1, UpdatedByUserId = actor.UserId.Value, UpdatedAtUtc = now });
            }
            foreach (var path in manifest.Assets)
            {
                var item = staged[path]; var name = Path.GetFileName(path);
                db.ProblemAssets.Add(new() { Id = Guid.NewGuid(), ProblemId = manifest.ProblemId, LogicalName = name, NormalizedName = name.ToUpperInvariant(), ContentType = ContentType(name), ObjectId = item.ObjectId, Sha256 = item.Sha256, Length = item.Length, CreatedByUserId = actor.UserId.Value, CreatedAtUtc = now });
            }
            AddReferenceSolution(manifest, staged, now);
            AddTests(manifest, staged, now);
            var import = new ProblemPackageImportRow { Id = Guid.NewGuid(), ProblemId = manifest.ProblemId, PackageSha256 = document.PackageSha256, IdempotencyKey = idempotencyKey, Mode = "CreateNew", CreatedAtUtc = now };
            db.ProblemPackageImports.Add(import); db.OutboxMessages.Add(ProblemOutbox.Create("PackageImported", manifest.ProblemId, now,
                new { problemId = manifest.ProblemId, importId = import.Id, mode = import.Mode }));
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); committed = true;
            foreach (var item in staged.Values.DistinctBy(x => x.ObjectId)) await objects.MarkReferencedAsync(item.ObjectId, cancellationToken);
            return Map(import);
        }
        catch { if (!committed) foreach (var item in staged.Values.DistinctBy(x => x.ObjectId)) await objects.DeleteStagedAsync(item.ObjectId, CancellationToken.None); throw; }
    }

    private void AddReferenceSolution(Mastemis.Mas.Packaging.Manifest.ProblemPackageManifest manifest, Dictionary<string, StagedProblemObject> staged, DateTimeOffset now)
    {
        if (manifest.ReferenceSolutions.Count == 0) return; var language = manifest.ReferenceSolutions[0].Language;
        if (manifest.ReferenceSolutions.Any(x => x.Language != language)) throw new ApplicationFailure(ErrorCodes.InvalidInput, "One reference solution language is supported per import.");
        var revision = new ReferenceSolutionRevisionRow { Id = Guid.NewGuid(), ProblemId = manifest.ProblemId, Language = language, CompileProfile = language == "cpp" ? "cpp23-o2-v1" : "dotnet-no-restore-v1", CreatedByUserId = actor.UserId.Value, CreatedAtUtc = now, IsCurrent = true, Enabled = true };
        db.ReferenceSolutionRevisions.Add(revision);
        foreach (var source in manifest.ReferenceSolutions) { var item = staged[source.Path]; db.ReferenceSolutionSources.Add(new() { RevisionId = revision.Id, FileName = Path.GetFileName(source.Path), ObjectId = item.ObjectId, Sha256 = item.Sha256, Length = item.Length }); }
    }
    private void AddTests(Mastemis.Mas.Packaging.Manifest.ProblemPackageManifest manifest, Dictionary<string, StagedProblemObject> staged, DateTimeOffset now)
    {
        if (manifest.Tests.Count == 0) return; var set = new GeneratedTestSetRow { Id = Guid.NewGuid(), ProblemId = manifest.ProblemId, GenerationOperationId = Guid.NewGuid(), Version = 1, Published = true, CreatedAtUtc = now, PublishedAtUtc = now }; db.GeneratedTestSets.Add(set);
        foreach (var test in manifest.Tests.OrderBy(x => x.Index)) { var input = staged[test.InputPath]; var output = staged[test.OutputPath!]; db.GeneratedTests.Add(new() { Id = Guid.NewGuid(), TestSetId = set.Id, TestIndex = test.Index, Group = test.GroupId, Visibility = manifest.Groups.Single(x => x.Id == test.GroupId).Visibility, Checker = manifest.Checker.Type, InputObjectId = input.ObjectId, InputSha256 = input.Sha256, InputLength = input.Length, OutputObjectId = output.ObjectId, OutputSha256 = output.Sha256, OutputLength = output.Length }); db.ProblemTestCases.Add(new() { Id = Guid.NewGuid(), ProblemId = manifest.ProblemId, TestIndex = test.Index, InputObjectId = input.ObjectId, ExpectedObjectId = output.ObjectId, InputBytes = input.Length, ExpectedBytes = output.Length, CheckerId = manifest.Checker.Type }); }
        db.ProblemJudgeProfiles.Add(new() { ProblemId = manifest.ProblemId, CpuMilliseconds = manifest.Limits.TimeMilliseconds, WallMilliseconds = Math.Max(manifest.Limits.TimeMilliseconds * 2, manifest.Limits.TimeMilliseconds + 250), MemoryBytes = manifest.Limits.MemoryBytes, OutputBytes = manifest.Limits.OutputBytes, FileBytes = Math.Max(manifest.Limits.OutputBytes, 67_108_864), ProcessCount = 16, TestCount = manifest.Tests.Count, CompilationMilliseconds = 30_000, CompilationOutputBytes = 4_194_304 });
    }
    private static IEnumerable<string> ReferencedPaths(Mastemis.Mas.Packaging.Archives.ProblemPackageDocument document) => document.Manifest.Assets.Concat(document.Manifest.ReferenceSolutions.Select(x => x.Path)).Concat(document.Manifest.Tests.SelectMany(x => new[] { x.InputPath, x.OutputPath! })).Distinct(StringComparer.Ordinal);
    private static string ReadMas(Mastemis.Mas.Packaging.Archives.ProblemPackageDocument document) => document.Manifest.Generators.FirstOrDefault(x => x.Language == "mas") is { } generator ? Encoding.UTF8.GetString(document.Entries[generator.Path]) : string.Empty;
    private static ProblemObjectKind Kind(string path) => path.StartsWith("assets/", StringComparison.Ordinal) ? ProblemObjectKind.Asset : path.StartsWith("solutions/", StringComparison.Ordinal) ? ProblemObjectKind.ReferenceSource : path.EndsWith(".out", StringComparison.Ordinal) ? ProblemObjectKind.ExpectedOutput : ProblemObjectKind.TestInput;
    private static long Maximum(ProblemObjectKind kind) => kind is ProblemObjectKind.Asset or ProblemObjectKind.ReferenceSource ? 4_194_304 : 67_108_864;
    private static string ContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp", _ => "text/plain" };
    private static string Sha256(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
    private static ProblemPackageImport Map(ProblemPackageImportRow row) => new(row.Id, row.ProblemId, row.PackageSha256, row.Mode);
}
