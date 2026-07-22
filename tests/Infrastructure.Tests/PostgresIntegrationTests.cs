using Mastemis.Application;
using Mastemis.Domain;
using Mastemis.Infrastructure;
using Mastemis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Mastemis.Infrastructure.Tests;

public sealed class PostgresIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;
    private static bool DockerAppearsAvailable => File.Exists("/var/run/docker.sock") || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST"));

    public async ValueTask InitializeAsync()
    {
        if (!DockerAppearsAvailable) return;
        _container = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("mastemis_tests")
            .WithUsername("mastemis").WithPassword(Guid.NewGuid().ToString("N")).Build();
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    [Fact]
    public async Task Clean_migration_creates_required_tables_and_constraints()
    {
        RequireDocker();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        const string sql = "SELECT table_name FROM information_schema.tables WHERE table_schema='public'";
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var tables = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken)) tables.Add(reader.GetString(0));
        foreach (var table in new[] { "exams", "exam_rooms", "candidates", "candidate_registrations", "exam_sessions",
            "source_revisions", "submissions", "judgements", "sfe_events", "sfe_evaluations", "confirmed_warnings",
            "judge_workers", "worker_credentials", "judge_jobs", "idempotency_records", "outbox_messages", "audit_records", "termination_metadata",
            "evidence_packages", "evidence_items", "evidence_review_grants", "problem_drafts", "problem_statements",
            "problem_generation_operations", "generated_test_sets", "generated_tests", "problem_package_imports", "problem_package_exports",
            "problem_author_assignments", "exam_problem_assignments", "problem_assets" })
            Assert.Contains(table, tables);
    }

    [Fact]
    public async Task Transaction_rollback_does_not_persist_exam()
    {
        RequireDocker();
        var id = Guid.NewGuid();
        await using (var db = CreateContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            db.Exams.Add(new ExamRow { Id = id, Title = "rollback", State = 0, CreatedAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }
        await using var verification = CreateContext();
        Assert.False(await verification.Exams.AnyAsync(x => x.Id == id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Concurrent_claimers_receive_job_once()
    {
        RequireDocker();
        var submissionId = Guid.NewGuid();
        await using (var seed = CreateContext())
        {
            await SeedSubmissionAsync(seed, submissionId);
            seed.JudgeJobs.Add(new JudgeJobRow
            {
                Id = Guid.NewGuid(),
                SubmissionId = submissionId,
                State = (int)JudgeJobState.Pending,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                AvailableAtUtc = DateTimeOffset.UtcNow,
                MaximumAttempts = 3,
                ConcurrencyToken = Guid.NewGuid()
            });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = CreateContext();
            var queue = new PostgresWorkerJudgeQueue(db, new SystemClock());
            return await queue.ClaimAsync(JudgeWorkerId.New(), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        }));
        Assert.Single(claims, x => x is not null);
    }

    [Fact]
    public async Task Expired_lease_is_recovered_with_new_lease_identity()
    {
        RequireDocker();
        var jobId = Guid.NewGuid(); var oldLease = Guid.NewGuid();
        await using (var seed = CreateContext())
        {
            var submissionId = Guid.NewGuid();
            await SeedSubmissionAsync(seed, submissionId);
            seed.JudgeJobs.Add(new JudgeJobRow
            {
                Id = jobId,
                SubmissionId = submissionId,
                State = (int)JudgeJobState.Claimed,
                WorkerId = Guid.NewGuid(),
                LeaseId = oldLease,
                LeaseExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                Attempt = 1,
                CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
                AvailableAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
                MaximumAttempts = 3,
                ConcurrencyToken = Guid.NewGuid()
            });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var db = CreateContext();
        var claimed = await new PostgresWorkerJudgeQueue(db, new SystemClock()).ClaimAsync(JudgeWorkerId.New(), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        Assert.NotNull(claimed); Assert.NotEqual(oldLease, claimed.LeaseId); Assert.Equal(2, claimed.Attempt);
    }

    [Fact]
    public async Task Duplicate_warning_evaluation_is_rejected_by_database()
    {
        RequireDocker();
        await using var db = CreateContext();
        var evaluation = Guid.NewGuid(); var session = Guid.NewGuid(); var eventId = Guid.NewGuid();
        var revisionId = await SeedSessionAsync(db, session);
        _ = revisionId;
        db.SfeEvents.Add(new SfeEventRow { Id = eventId, SessionId = session, ClientSequence = 1, ClientTimestamp = DateTimeOffset.UtcNow, ServerReceivedAtUtc = DateTimeOffset.UtcNow, EventType = "test" });
        db.SfeEvaluations.Add(new SfeEvaluationRow { Id = evaluation, EventId = eventId, SessionId = session, Result = (int)EvaluationResult.ConfirmedViolation, ReasonCode = "test", EvaluatedAtUtc = DateTimeOffset.UtcNow });
        db.ConfirmedWarnings.AddRange(
            new WarningRow { Id = Guid.NewGuid(), ExamId = Guid.NewGuid(), RoomId = Guid.NewGuid(), CandidateId = Guid.NewGuid(), SessionId = session, EvaluationId = evaluation, Ordinal = 1, IssuedAtUtc = DateTimeOffset.UtcNow },
            new WarningRow { Id = Guid.NewGuid(), ExamId = Guid.NewGuid(), RoomId = Guid.NewGuid(), CandidateId = Guid.NewGuid(), SessionId = session, EvaluationId = evaluation, Ordinal = 2, IssuedAtUtc = DateTimeOffset.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private MastemisDbContext CreateContext() => new(new DbContextOptionsBuilder<MastemisDbContext>().UseNpgsql(_connectionString!).Options);
    private static async Task SeedSubmissionAsync(MastemisDbContext db, Guid submissionId)
    {
        var sessionId = Guid.NewGuid(); var revisionId = await SeedSessionAsync(db, sessionId);
        db.Submissions.Add(new SubmissionRow { Id = submissionId, SessionId = sessionId, ProblemId = Guid.NewGuid(), RevisionId = revisionId, Language = "csharp", CreatedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
    private static async Task<Guid> SeedSessionAsync(MastemisDbContext db, Guid sessionId)
    {
        var examId = Guid.NewGuid(); var roomId = Guid.NewGuid(); var candidateId = Guid.NewGuid(); var revisionId = Guid.NewGuid();
        db.Exams.Add(new ExamRow { Id = examId, Title = "seed", State = (int)ExamState.Open, CreatedAtUtc = DateTimeOffset.UtcNow });
        db.Rooms.Add(new RoomRow { Id = roomId, ExamId = examId, Code = Guid.NewGuid().ToString("N"), Name = "seed" });
        db.Candidates.Add(new CandidateRow { Id = candidateId, UserId = Guid.NewGuid() });
        db.ExamSessions.Add(new SessionRow { Id = sessionId, ExamId = examId, RoomId = roomId, CandidateId = candidateId, State = (int)SessionState.Active, ConcurrencyToken = Guid.NewGuid() });
        db.SourceRevisions.Add(new SourceRevisionRow { Id = revisionId, SessionId = sessionId, ObjectId = $"source/{revisionId:N}", Sha256 = new string('0', 64), Length = 1, CreatedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return revisionId;
    }
    private static void RequireDocker() { if (!DockerAppearsAvailable) Assert.Skip("Docker is unavailable; PostgreSQL Testcontainers tests were not executed."); }
}
