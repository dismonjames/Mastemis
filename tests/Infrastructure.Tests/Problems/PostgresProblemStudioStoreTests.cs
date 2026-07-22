using System.Security.Cryptography;
using Mastemis.Application;
using Mastemis.Application.Problems.Assets;
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
            var store = new PostgresProblemStudioStore(db, storage, clock);
            var draft = await store.CreateAsync("Durable", "en", TestContext.Current.CancellationToken); problemId = draft.Id.Value;
            const string source = "test 1 { input = int(1, 1) }";
            await store.SaveMasAsync(draft.Id, source, Hex(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source))), TestContext.Current.CancellationToken);
            var operation = await store.BeginGenerationAsync(draft.Id, 42, "mas-runtime/1.0", TestContext.Current.CancellationToken); operationId = operation.Id;
            var input = "1\n"u8.ToArray();
            await store.PublishTestsAsync(operation, [(1, "default", input, Hex(SHA256.HashData(input)))], TestContext.Current.CancellationToken);
        }
        await using var verification = Context();
        Assert.Equal("Durable", (await verification.ProblemDrafts.SingleAsync(x => x.Id == problemId, TestContext.Current.CancellationToken)).Title);
        Assert.Equal(2, (await verification.ProblemGenerationOperations.SingleAsync(x => x.Id == operationId, TestContext.Current.CancellationToken)).Status);
        var test = await verification.GeneratedTests.SingleAsync(TestContext.Current.CancellationToken);
        await using var inputStream = await storage.OpenReadAsync(test.InputObjectId, 100, TestContext.Current.CancellationToken);
        Assert.Equal("1\n", await new StreamReader(inputStream).ReadToEndAsync(TestContext.Current.CancellationToken));
        Assert.Contains(await verification.OutboxMessages.Select(x => x.Type).ToListAsync(TestContext.Current.CancellationToken), x => x == "GeneratedTestSetPublished");
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
        if (Directory.Exists(_objects)) Directory.Delete(_objects, true);
    }

    private MastemisDbContext Context() => new(new DbContextOptionsBuilder<MastemisDbContext>().UseNpgsql(_connectionString!).Options);
    private static string Hex(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
}
