using System.Text;
using System.Text.Json;
using Mastemis.Application;
using Mastemis.Application.Problems.Assets;
using Mastemis.Application.Problems.Packages;
using Mastemis.Application.Problems.Statements;
using Mastemis.Mas.Packaging.Archives;
using Mastemis.Mas.Packaging.Exporting;
using Mastemis.Mas.Packaging.Importing;
using Mastemis.Mas.Packaging.Manifest;
using Mastemis.Mas.Packaging.Security;
using Mastemis.Mas.Packaging.Validation;
using Mastemis.Mas.Packaging.Versions;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class PostgresProblemPackageService(MastemisDbContext db, IProblemObjectStorage objects,
    IAuthorizationService authorization) : IProblemPackageService
{
    private static readonly PackageArchiveLimits Limits = new();
    public async Task<ProblemPackageValidation> ValidateAsync(Stream package, CancellationToken cancellationToken)
    {
        var result = await new ProblemPackageImporter(new PackageArchiveReader(Limits), new ProblemPackageValidator())
            .InspectAsync(package, cancellationToken);
        return new(result.Package.PackageSha256, result.Diagnostics);
    }

    public async Task<ProblemPackageExport> ExportAsync(Guid problemId, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("problem.hidden", problemId, cancellationToken);
        var draft = await db.ProblemDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == problemId, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem not found.");
        var statements = await db.ProblemStatements.AsNoTracking().Where(x => x.ProblemId == problemId).OrderBy(x => x.Locale).ToArrayAsync(cancellationToken);
        if (statements.All(x => x.Locale != draft.DefaultLocale)) throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "The default localized statement is required.");
        var content = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var statementPaths = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var statement in statements)
        {
            var bytes = await ReadAsync(statement.ObjectId, statement.Length, cancellationToken);
            var structured = JsonSerializer.Deserialize<ProblemStatementContent>(bytes) ?? throw new ApplicationFailure(ErrorCodes.InvalidInput, "Stored statement is invalid.");
            var path = $"statement/{statement.Locale}.md"; statementPaths[statement.Locale] = path;
            content[path] = Encoding.UTF8.GetBytes(structured.Markdown);
        }
        var assets = await db.ProblemAssets.AsNoTracking().Where(x => x.ProblemId == problemId).OrderBy(x => x.NormalizedName).ToArrayAsync(cancellationToken);
        foreach (var asset in assets) content[$"assets/{asset.LogicalName}"] = await ReadAsync(asset.ObjectId, asset.Length, cancellationToken);
        var revision = await db.ReferenceSolutionRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.ProblemId == problemId && x.IsCurrent && x.Enabled, cancellationToken);
        var solutions = new List<SourceEntryManifest>();
        if (revision is not null)
        {
            var sources = await db.ReferenceSolutionSources.AsNoTracking().Where(x => x.RevisionId == revision.Id).OrderBy(x => x.FileName).ToArrayAsync(cancellationToken);
            foreach (var source in sources) { var path = $"solutions/{source.FileName}"; content[path] = await ReadAsync(source.ObjectId, source.Length, cancellationToken); solutions.Add(new(source.FileName, revision.Language, path)); }
        }
        if (!string.IsNullOrWhiteSpace(draft.MasSource)) content["generators/main.mas"] = Encoding.UTF8.GetBytes(draft.MasSource);
        var set = await db.GeneratedTestSets.AsNoTracking().Where(x => x.ProblemId == problemId && x.Published).OrderByDescending(x => x.Version).FirstOrDefaultAsync(cancellationToken);
        var tests = new List<TestFileManifest>(); var groups = new List<TestGroupManifest>();
        if (set is not null)
        {
            var rows = await db.GeneratedTests.AsNoTracking().Where(x => x.TestSetId == set.Id).OrderBy(x => x.TestIndex).ToArrayAsync(cancellationToken);
            groups.AddRange(rows.Select(x => x.Group).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal)
                .Select((name, index) => new TestGroupManifest(name, rows.First(x => x.Group == name).Visibility, index, 1)));
            foreach (var row in rows)
            {
                var inputPath = $"tests/{row.Visibility}/{row.TestIndex:D5}.in"; var outputPath = $"tests/{row.Visibility}/{row.TestIndex:D5}.out";
                content[inputPath] = await ReadAsync(row.InputObjectId, row.InputLength, cancellationToken);
                if (row.OutputObjectId is null || row.OutputLength is null) throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "Published expected output is missing.");
                content[outputPath] = await ReadAsync(row.OutputObjectId, row.OutputLength.Value, cancellationToken);
                tests.Add(new(row.Id.ToString("N"), row.Group, row.TestIndex, inputPath, outputPath, row.InputLength, row.OutputLength));
            }
        }
        var manifest = new ProblemPackageManifest(PackageFormat.CurrentVersion, problemId, draft.Title, [], [], "unspecified",
            draft.DefaultLocale, statementPaths, new(draft.TimeLimitMilliseconds, draft.MemoryLimitBytes, draft.OutputLimitBytes),
            revision is null ? ["cpp", "csharp"] : [revision.Language], new(draft.Checker), groups, tests,
            string.IsNullOrWhiteSpace(draft.MasSource) ? [] : [new("main", "mas", "generators/main.mas")], solutions,
            assets.Select(x => $"assets/{x.LogicalName}").ToArray(), new Dictionary<string, string>());
        var destination = new MemoryStream(); var result = await new ProblemPackageExporter().ExportAsync(manifest, content, destination, cancellationToken);
        destination.Position = 0; return new(destination, result.Sha256, result.Length);
    }

    private async Task<byte[]> ReadAsync(string objectId, long length, CancellationToken cancellationToken)
    { await using var stream = await objects.OpenReadAsync(objectId, length, cancellationToken); using var output = new MemoryStream(); await stream.CopyToAsync(output, cancellationToken); return output.ToArray(); }
}
