using System.Security.Cryptography;
using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Application.Problems.Assets;
using Mastemis.Application.Problems.Authoring;
using Mastemis.Contracts.Judge;
using Mastemis.Contracts.Problems.ReferenceOutputs;
using Mastemis.Domain;
using Mastemis.Infrastructure.Persistence;
using Mastemis.Infrastructure.Persistence.Problems;
using Mastemis.Infrastructure.Storage.ProblemObjects;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Mastemis.Infrastructure.Tests.Problems;

public sealed class PostgresProblemStudioStoreTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;
    private readonly string _objects = Path.Combine(Path.GetTempPath(), $"mastemis-problem-store-{Guid.NewGuid():N}");
    private static bool DockerAvailable => File.Exists("/var/run/docker.sock") || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST"));

    public async ValueTask InitializeAsync()
    {
        if (!DockerAvailable) return;
        _container = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("problem_tests")
            .WithUsername("mastemis").WithPassword(Guid.NewGuid().ToString("N")).Build();
        await _container.StartAsync(); _connectionString = _container.GetConnectionString();
        await using var db = Context(); await db.Database.MigrateAsync();
    }

    [Fact]
    public async Task Draft_generation_and_published_tests_survive_context_restart()
    {
        if (!DockerAvailable) Assert.Skip("Docker is unavailable; PostgreSQL Problem Studio storage was not executed.");
        var clock = new Clock(); var storage = new FileProblemObjectStorage(_objects, clock);
        Guid operationId; Guid problemId;
        await using (var db = Context())
        {
            var actor = new Actor();
            db.Users.Add(new ApplicationUser { Id = actor.UserId.Value, UserName = "problem-owner", NormalizedUserName = "PROBLEM-OWNER", DisplayName = "Problem Owner", CreatedAtUtc = clock.UtcNow });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var store = new PostgresProblemStudioStore(db, storage, clock, actor);
            var draft = await store.CreateAsync("Durable", "en", TestContext.Current.CancellationToken); problemId = draft.Id.Value;
            const string source = "test 1 { input = int(1, 1) }";
            await store.SaveMasAsync(draft.Id, source, Hex(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source))), TestContext.Current.CancellationToken);
            var operation = await store.BeginGenerationAsync(draft.Id, 42, "mas-runtime/1.0", TestContext.Current.CancellationToken); operationId = operation.Id;
            operation = await store.TransitionGenerationAsync(operation.Id, GenerationOperationStatus.Validating, 0, 1, TestContext.Current.CancellationToken);
            operation = await store.TransitionGenerationAsync(operation.Id, GenerationOperationStatus.GeneratingInputs, 0, 1, TestContext.Current.CancellationToken);
            var input = "1\n"u8.ToArray();
            await store.StageInputsAsync(operation, [(1, "default", input, Hex(SHA256.HashData(input)))], TestContext.Current.CancellationToken);
        }
        await using var verification = Context();
        Assert.Equal("Durable", (await verification.ProblemDrafts.SingleAsync(x => x.Id == problemId, TestContext.Current.CancellationToken)).Title);
        Assert.Equal((int)GenerationOperationStatus.WaitingForReferenceOutputs, (await verification.ProblemGenerationOperations.SingleAsync(x => x.Id == operationId, TestContext.Current.CancellationToken)).Status);
        var test = await verification.GeneratedTests.SingleAsync(TestContext.Current.CancellationToken);
        var stagedPath = Path.Combine(_objects, ".staged", "test-input", $"{test.InputObjectId.Split('/')[2]}.bin");
        Assert.Equal("1\n", await File.ReadAllTextAsync(stagedPath, TestContext.Current.CancellationToken));
        Assert.Contains(await verification.OutboxMessages.Select(x => x.Type).ToListAsync(TestContext.Current.CancellationToken), x => x == "GenerationWaitingForReferenceOutputs");
    }

    [Fact]
    public async Task Concurrent_reference_claimers_receive_job_once()
    {
        if (!DockerAvailable) Assert.Skip("Docker is unavailable; PostgreSQL reference queue was not executed.");
        var operation = Guid.NewGuid(); var problem = Guid.NewGuid(); var worker = JudgeWorkerId.New();
        await using (var db = Context())
        {
            db.ProblemDrafts.Add(new() { Id = problem, Title = "Reference", DefaultLocale = "en", CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow, ConcurrencyToken = Guid.NewGuid() });
            db.ProblemGenerationOperations.Add(new() { Id = operation, ProblemId = problem, DraftVersion = 1, ActorUserId = Guid.NewGuid(), Status = 3, Seed = 1, RuntimeVersion = "1", CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow, ConcurrencyToken = Guid.NewGuid() });
            db.JudgeWorkers.Add(new() { Id = worker.Value, Name = "reference", Capacity = 4, IsEnabled = true, CreatedAtUtc = DateTimeOffset.UtcNow, LanguagesJson = "[\"cpp\"]" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await new PostgresReferenceOutputQueue(db, new Clock()).EnqueueAsync(Payload(operation, problem), 3, TestContext.Current.CancellationToken);
        }
        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        { await using var db = Context(); return await new PostgresReferenceOutputQueue(db, new Clock()).ClaimAsync(worker, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken); }));
        Assert.Single(claims, x => x is not null);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
        if (Directory.Exists(_objects)) Directory.Delete(_objects, true);
    }

    private MastemisDbContext Context() => new(new DbContextOptionsBuilder<MastemisDbContext>().UseNpgsql(_connectionString!).Options);
    private static string Hex(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();
    private static ReferenceOutputJobPayload Payload(Guid operation, Guid problem) => new(1, Guid.NewGuid(), operation, new(problem), 1,
        Guid.NewGuid(), "cpp", [new("main.cpp", "problem/reference-source/00000000000000000000000000000000", new string('a', 64), 1)],
        [new(1, "problem/test-input/00000000000000000000000000000000", new string('b', 64), 1)],
        new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 64 * 1024 * 1024, 1024, 1024 * 1024, 4, 1,
            TimeSpan.FromSeconds(10), 1024 * 1024), TimeSpan.FromMinutes(1));
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
    private sealed class Actor : IAdministrationActor
    {
        public UserId UserId { get; } = new(Guid.NewGuid());
        public bool IsInRole(string role) => role == MastemisRoles.Administrator;
    }
}
