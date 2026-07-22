using Mastemis.Application;
using Mastemis.Domain;
using Mastemis.Infrastructure;
using Mastemis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Mastemis.Infrastructure.Tests.Sfe;

public sealed class TerminationRaceTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;
    private static bool DockerAvailable => File.Exists("/var/run/docker.sock") ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST"));

    public async ValueTask InitializeAsync()
    {
        if (!DockerAvailable) return;
        _container = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("mastemis_races")
            .WithUsername("mastemis").WithPassword(Guid.NewGuid().ToString("N")).Build();
        await _container.StartAsync(); _connectionString = _container.GetConnectionString();
        await using var db = CreateContext(); await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() { if (_container is not null) await _container.DisposeAsync(); }

    [Fact]
    public async Task Two_application_instances_processing_third_warning_create_one_final_state()
    {
        RequireDocker(); var state = await SeedAsync(); using var gate = new Barrier(2);
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            gate.SignalAndWait(TestContext.Current.CancellationToken);
            try { await IssueAsync(state, "same-retry-key"); return "success"; }
            catch (ApplicationFailure failure) { return failure.Code; }
        }, TestContext.Current.CancellationToken)));
        Assert.Contains("success", outcomes); await AssertTerminationInvariantsAsync(state);
    }

    [Fact]
    public async Task Retry_after_successful_commit_does_not_duplicate_durable_state()
    {
        RequireDocker(); var state = await SeedAsync(); await IssueAsync(state, "retry-key");
        await Assert.ThrowsAsync<ApplicationFailure>(() => IssueAsync(state, "retry-key"));
        await AssertTerminationInvariantsAsync(state);
    }

    [Fact]
    public async Task Third_warning_racing_ordinary_submission_preserves_single_final_submission()
    {
        RequireDocker(); var state = await SeedAsync(); using var gate = new Barrier(2);
        var warning = Task.Run(async () => { gate.SignalAndWait(TestContext.Current.CancellationToken); try { await IssueAsync(state, "warning-submit-race"); } catch (ApplicationFailure) { } }, TestContext.Current.CancellationToken);
        var submit = Task.Run(async () =>
        {
            gate.SignalAndWait(TestContext.Current.CancellationToken);
            try { await Service().CreateSubmissionAsync(new(new(state.SessionId), new(state.ProblemId), new(state.RevisionId), "csharp", "ordinary-race"), TestContext.Current.CancellationToken); }
            catch (Exception exception) when (exception is ApplicationFailure or DomainException) { }
        }, TestContext.Current.CancellationToken);
        await Task.WhenAll(warning, submit); await AssertTerminationInvariantsAsync(state);
    }

    [Fact]
    public async Task Duplicate_raw_event_sequence_is_rejected_without_extra_warning()
    {
        RequireDocker(); var state = await SeedAsync();
        await using var first = CreateContext(); first.SfeEvents.Add(Event(state.SessionId, 99)); await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var duplicate = CreateContext(); duplicate.SfeEvents.Add(Event(state.SessionId, 99));
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync(TestContext.Current.CancellationToken));
        await using var verify = CreateContext(); Assert.Equal(2, await verify.ConfirmedWarnings.CountAsync(x => x.SessionId == state.SessionId, TestContext.Current.CancellationToken));
    }

    private async Task<RaceState> SeedAsync()
    {
        var state = new RaceState(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var db = CreateContext(); var now = FixedClock.Now;
        db.Exams.Add(new ExamRow { Id = state.ExamId, Title = "race", State = (int)ExamState.Open, CreatedAtUtc = now });
        db.Rooms.Add(new RoomRow { Id = state.RoomId, ExamId = state.ExamId, Code = state.RoomId.ToString("N"), Name = "race" });
        db.Candidates.Add(new CandidateRow { Id = state.CandidateId, UserId = Guid.NewGuid() });
        db.CandidateRegistrations.Add(new CandidateRegistrationRow
        {
            Id = Guid.NewGuid(),
            ExamId = state.ExamId,
            CandidateId = state.CandidateId,
            RegistrationCode = Guid.NewGuid().ToString("N"),
            AccessState = (int)CandidateExamAccessState.Enabled,
            CreatedAtUtc = now
        });
        db.ExamSessions.Add(new SessionRow
        {
            Id = state.SessionId,
            ExamId = state.ExamId,
            RoomId = state.RoomId,
            CandidateId = state.CandidateId,
            State = (int)SessionState.Active,
            CurrentRevisionId = state.RevisionId,
            ConcurrencyToken = Guid.NewGuid()
        });
        db.SourceRevisions.Add(new SourceRevisionRow
        {
            Id = state.RevisionId,
            SessionId = state.SessionId,
            ObjectId = $"source/{state.RevisionId:N}",
            Sha256 = new string('0', 64),
            Length = 1,
            CreatedAtUtc = now
        });
        for (var ordinal = 1; ordinal <= 3; ordinal++)
        {
            var eventId = Guid.NewGuid(); var evaluationId = ordinal == 3 ? state.ThirdEvaluationId : Guid.NewGuid();
            db.SfeEvents.Add(Event(state.SessionId, ordinal, eventId));
            db.SfeEvaluations.Add(new SfeEvaluationRow
            {
                Id = evaluationId,
                EventId = eventId,
                SessionId = state.SessionId,
                Result = (int)EvaluationResult.ConfirmedViolation,
                ReasonCode = "baseline.concurrent",
                EvaluatedAtUtc = now
            });
            if (ordinal < 3) db.ConfirmedWarnings.Add(new WarningRow
            {
                Id = Guid.NewGuid(),
                ExamId = state.ExamId,
                RoomId = state.RoomId,
                CandidateId = state.CandidateId,
                SessionId = state.SessionId,
                EvaluationId = evaluationId,
                Ordinal = ordinal,
                IssuedAtUtc = now
            });
        }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken); return state;
    }

    private async Task IssueAsync(RaceState state, string key) => await Service().IssueStoredWarningAsync(
        new(new(state.SessionId), new(state.ThirdEvaluationId), new(state.ProblemId), "csharp", key), TestContext.Current.CancellationToken);

    private MastemisService Service()
    {
        var db = CreateContext(); var runtime = new PostgresRuntime(db, new FixedClock());
        return new(runtime, runtime, new FixedClock(), new AllowAuthorization(), new MemorySourceStorage(),
            new LegacyDurableJudgeQueue(db, new FixedClock()), runtime);
    }

    private async Task AssertTerminationInvariantsAsync(RaceState state)
    {
        await using var db = CreateContext();
        var session = await db.ExamSessions.SingleAsync(x => x.Id == state.SessionId, TestContext.Current.CancellationToken);
        Assert.Equal((int)SessionState.Terminated, session.State); Assert.Equal(state.RevisionId, session.FrozenRevisionId);
        var ordinals = await db.ConfirmedWarnings.Where(x => x.SessionId == state.SessionId).OrderBy(x => x.Ordinal)
            .Select(x => x.Ordinal).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { 1, 2, 3 }, ordinals);
        var final = await db.Submissions.SingleAsync(x => x.SessionId == state.SessionId && x.IsFinal, TestContext.Current.CancellationToken);
        Assert.Equal(1, await db.JudgeJobs.CountAsync(x => x.SubmissionId == final.Id, TestContext.Current.CancellationToken));
        Assert.Equal((int)CandidateExamAccessState.Terminated, await db.CandidateRegistrations.Where(x => x.ExamId == state.ExamId && x.CandidateId == state.CandidateId).Select(x => x.AccessState).SingleAsync(TestContext.Current.CancellationToken));
        Assert.True(await db.OutboxMessages.AnyAsync(x => x.ResourceId == state.SessionId.ToString("D"), TestContext.Current.CancellationToken));
    }

    private static SfeEventRow Event(Guid sessionId, long sequence, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        SessionId = sessionId,
        ClientSequence = sequence,
        ClientTimestamp = FixedClock.Now,
        ServerReceivedAtUtc = FixedClock.Now,
        EventType = "ConcurrentSessionDetected"
    };
    private MastemisDbContext CreateContext() => new(new DbContextOptionsBuilder<MastemisDbContext>().UseNpgsql(_connectionString!).Options);
    private static void RequireDocker() { if (!DockerAvailable) Assert.Skip("Docker is unavailable; PostgreSQL termination race tests were not executed."); }
    private sealed record RaceState(Guid ExamId, Guid RoomId, Guid CandidateId, Guid SessionId, Guid RevisionId, Guid ProblemId, Guid ThirdEvaluationId);
    private sealed class FixedClock : IClock { public static DateTimeOffset Now { get; } = new(2026, 7, 22, 7, 0, 0, TimeSpan.Zero); public DateTimeOffset UtcNow => Now; }
    private sealed class AllowAuthorization : Mastemis.Application.IAuthorizationService { public ValueTask EnsureAsync(string permission, Guid scopeId, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.CompletedTask; } }
    private sealed class MemorySourceStorage : ISourceRevisionStorage { public Task<StoredSourceRevision> StoreAsync(SourceRevisionId id, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) => Task.FromResult(new StoredSourceRevision($"source/{id.Value:N}", new string('0', 64), content.Length)); }
}
