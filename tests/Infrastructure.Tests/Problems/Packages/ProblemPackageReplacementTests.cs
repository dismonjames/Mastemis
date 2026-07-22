using System.Text;
using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Domain;
using Mastemis.Infrastructure.Persistence;
using Mastemis.Infrastructure.Persistence.Problems;
using Mastemis.Infrastructure.Storage.ProblemObjects;
using Mastemis.Mas.Packaging.Exporting;
using Mastemis.Mas.Packaging.Manifest;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Mastemis.Infrastructure.Tests.Problems.Packages;

public sealed class ProblemPackageReplacementTests : IAsyncLifetime
{
    private static readonly bool DockerAvailable = Fixtures.DockerEnvironment.IsAvailable();
    private readonly Guid actorId = Guid.NewGuid();
    private readonly string objectRoot = Path.Combine(Path.GetTempPath(), $"mastemis-replace-{Guid.NewGuid():N}");
    private PostgreSqlContainer? container;
    private string? connectionString;

    public async ValueTask InitializeAsync()
    {
        if (!DockerAvailable) return;
        container = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("package_replace")
            .WithUsername("mastemis").WithPassword(Guid.NewGuid().ToString("N")).Build();
        await container.StartAsync(); connectionString = container.GetConnectionString();
        await using var db = Context(); await db.Database.MigrateAsync();
        db.Users.Add(new ApplicationUser
        {
            Id = actorId,
            UserName = "package-owner",
            NormalizedUserName = "PACKAGE-OWNER",
            DisplayName = "Package Owner",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ReplaceDraft_persists_complete_state_and_retries_idempotently()
    {
        RequireDocker(); var problemId = Guid.NewGuid();
        await SeedDraftAsync(problemId);
        var package = await PackageAsync(problemId, "Replacement");

        await using (var db = Context())
        {
            var replacer = Replacer(db);
            await using var stream = new MemoryStream(package, false);
            var result = await replacer.ReplaceAsync(problemId, 1, stream, "replace-once", TestContext.Current.CancellationToken);
            await using var ignored = Stream.Null;
            var retry = await replacer.ReplaceAsync(problemId, 1, ignored, "replace-once", TestContext.Current.CancellationToken);
            Assert.Equal(result.ImportId, retry.ImportId);
        }

        await using var verification = Context();
        var draft = await verification.ProblemDrafts.SingleAsync(x => x.Id == problemId, TestContext.Current.CancellationToken);
        Assert.Equal("Replacement", draft.Title);
        Assert.Equal(2, draft.Version);
        Assert.Equal("test 1 { input = int(1, 1) }", draft.MasSource);
        Assert.Single(await verification.ProblemStatements.Where(x => x.ProblemId == problemId).ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Single(await verification.ReferenceSolutionRevisions.Where(x => x.ProblemId == problemId && x.IsCurrent).ToArrayAsync(TestContext.Current.CancellationToken));
        var test = await verification.GeneratedTests.SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(test.OutputObjectId);
        Assert.Single(await verification.ProblemPackageImports.Where(x => x.ProblemId == problemId).ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReplaceDraft_rejects_open_examination_without_visible_mutation()
    {
        RequireDocker(); var problemId = Guid.NewGuid(); var examId = Guid.NewGuid();
        await SeedDraftAsync(problemId);
        await using (var db = Context())
        {
            db.Exams.Add(new ExamRow { Id = examId, Title = "Open", State = (int)ExamState.Open });
            db.ExamProblemAssignments.Add(new() { ExamId = examId, ProblemId = problemId, AssignedAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var package = await PackageAsync(problemId, "Forbidden replacement");
        await using (var db = Context())
        await using (var stream = new MemoryStream(package, false))
        {
            var failure = await Assert.ThrowsAsync<ApplicationFailure>(async () =>
                await Replacer(db).ReplaceAsync(problemId, 1, stream, "replace-open", TestContext.Current.CancellationToken));
            Assert.Equal(ErrorCodes.Forbidden, failure.Code);
        }
        await using var verification = Context();
        Assert.Equal("Original", (await verification.ProblemDrafts.SingleAsync(x => x.Id == problemId,
            TestContext.Current.CancellationToken)).Title);
        Assert.Empty(await verification.ProblemPackageImports.Where(x => x.ProblemId == problemId)
            .ToArrayAsync(TestContext.Current.CancellationToken));
    }

    public async ValueTask DisposeAsync()
    {
        if (container is not null) await container.DisposeAsync();
        if (Directory.Exists(objectRoot)) Directory.Delete(objectRoot, true);
    }

    private async Task SeedDraftAsync(Guid problemId)
    {
        await using var db = Context(); var now = DateTimeOffset.UtcNow;
        db.ProblemDrafts.Add(new()
        {
            Id = problemId,
            Title = "Original",
            DefaultLocale = "en",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid(),
            Version = 1
        });
        db.ProblemAuthorAssignments.Add(new()
        {
            ProblemId = problemId,
            UserId = actorId,
            Role = 0,
            Status = 0,
            AssignedByUserId = actorId,
            AssignedAtUtc = now
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<byte[]> PackageAsync(Guid problemId, string title)
    {
        var content = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["statement/en.md"] = Encoding.UTF8.GetBytes($"# {title}"),
            ["generators/main.mas"] = "test 1 { input = int(1, 1) }"u8.ToArray(),
            ["solutions/Program.cs"] = "Console.WriteLine(Console.ReadLine());"u8.ToArray(),
            ["tests/hidden/00001.in"] = "1\n"u8.ToArray(),
            ["tests/hidden/00001.out"] = "1\n"u8.ToArray()
        };
        var manifest = new ProblemPackageManifest("1.0", problemId, title, ["Owner"], ["smoke"], "easy", "en",
            new Dictionary<string, string> { ["en"] = "statement/en.md" }, new(1000, 67_108_864, 1_048_576), ["csharp"],
            new("exact"), [new("hidden", "hidden", 0, 1)],
            [new("test-1", "hidden", 1, "tests/hidden/00001.in", "tests/hidden/00001.out", 2, 2)],
            [new("main", "mas", "generators/main.mas")], [new("reference", "csharp", "solutions/Program.cs")], [],
            new Dictionary<string, string>());
        await using var output = new MemoryStream();
        await new ProblemPackageExporter().ExportAsync(manifest, content, output, TestContext.Current.CancellationToken);
        return output.ToArray();
    }

    private PostgresProblemPackageReplacer Replacer(MastemisDbContext db) => new(db,
        new FileProblemObjectStorage(objectRoot, new Clock()), new AllowAuthorization(), new Actor(actorId), new Clock());

    private MastemisDbContext Context() => new(new DbContextOptionsBuilder<MastemisDbContext>()
        .UseNpgsql(connectionString!).Options);

    private static void RequireDocker()
    {
        if (!DockerAvailable) Assert.Skip("Docker is unavailable; transactional PostgreSQL package replacement was not executed.");
    }

    private sealed class Clock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
    private sealed class Actor(Guid id) : IAdministrationActor
    {
        public UserId UserId { get; } = new(id);
        public bool IsInRole(string role) => role == MastemisRoles.Administrator;
    }
    private sealed class AllowAuthorization : IAuthorizationService
    {
        public ValueTask EnsureAsync(string permission, Guid scopeId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }
}
