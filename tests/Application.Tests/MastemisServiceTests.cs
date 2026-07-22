using Mastemis.Application;
using Mastemis.Domain;

namespace Mastemis.Application.Tests;

public sealed class MastemisServiceTests
{
    [Fact]
    public async Task Vertical_flow_creates_exam_room_session_draft_submission_and_queue_job()
    {
        var fake = new TestRuntime();
        var service = fake.CreateService();
        var exam = await service.CreateExamAsync(new("Regional", "exam-1"), TestContext.Current.CancellationToken);
        var room = await service.CreateRoomAsync(new(exam.Id, "A", "room-1"), TestContext.Current.CancellationToken);
        var candidate = await service.RegisterCandidateAsync(new(exam.Id, UserId.New(), "C01", "candidate-1"), TestContext.Current.CancellationToken);
        await service.OpenExamAsync(exam.Id, TestContext.Current.CancellationToken);
        var session = await service.StartExamSessionAsync(new(exam.Id, room.Id, candidate.Id, "session-1"), TestContext.Current.CancellationToken);
        var revision = await service.SaveDraftRevisionAsync(new(session.Id, "int main(){}"u8.ToArray(), "draft-1"), TestContext.Current.CancellationToken);
        var submission = await service.CreateSubmissionAsync(new(session.Id, ProblemId.New(), revision.Id, "cpp", "submission-1"), TestContext.Current.CancellationToken);
        Assert.Equal(revision.Id, submission.RevisionId);
        Assert.Single(fake.Jobs);
        Assert.Contains(fake.Outbox, item => item is DraftSaved);
        Assert.Contains(fake.Outbox, item => item is SubmissionCreated);
    }

    [Fact]
    public async Task Raw_event_is_server_timestamped_and_connection_loss_is_not_violation()
    {
        var fake = new TestRuntime();
        var (service, session) = await StartedSessionAsync(fake);
        var activityEvent = await service.RecordRawSfeEventAsync(new(session.Id, 1, DateTimeOffset.MinValue,
            "ConnectionLost", new Dictionary<string, string>(), "event-1"), TestContext.Current.CancellationToken);
        var evaluation = await service.EvaluateSfeEventAsync(new(activityEvent.Id, null, false), TestContext.Current.CancellationToken);
        Assert.Equal(fake.Now, activityEvent.ServerReceivedAtUtc);
        Assert.Equal(EvaluationResult.Recorded, evaluation.Result);
        Assert.Empty(session.Warnings);
    }

    [Fact]
    public async Task Third_confirmed_violation_atomically_freezes_source_and_enqueues_one_final_job()
    {
        var fake = new TestRuntime();
        var (service, session) = await StartedSessionAsync(fake);
        var revision = SourceRevisionId.New();
        for (var index = 0; index < 3; index++)
        {
            var evaluation = new ViolationEvaluation(ViolationEvaluationId.New(), ViolationEventId.New(), session.Id,
                EvaluationResult.ConfirmedViolation, "test", fake.Now);
            await service.ConfirmViolationAsync(new(session.Id, evaluation.Id, revision, ProblemId.New(), "csharp", $"warning-{index}"),
                evaluation, TestContext.Current.CancellationToken);
        }
        Assert.Equal(SessionState.Terminated, session.State);
        Assert.Equal(revision, session.FrozenRevisionId);
        Assert.Single(fake.Submissions.Values, x => x.IsFinal);
        Assert.Single(fake.Jobs);
        Assert.Single(fake.Outbox.OfType<SessionTerminated>());
    }

    [Fact]
    public async Task Idempotency_key_reuse_is_rejected()
    {
        var fake = new TestRuntime();
        var service = fake.CreateService();
        await service.CreateExamAsync(new("One", "same"), TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<ApplicationFailure>(() => service.CreateExamAsync(new("Two", "same"), TestContext.Current.CancellationToken));
        Assert.Equal(ErrorCodes.IdempotencyConflict, error.Code);
    }

    private static async Task<(MastemisService Service, ExamSession Session)> StartedSessionAsync(TestRuntime fake)
    {
        var service = fake.CreateService();
        var exam = await service.CreateExamAsync(new("Exam", Guid.NewGuid().ToString()), TestContext.Current.CancellationToken);
        var room = await service.CreateRoomAsync(new(exam.Id, "Room", Guid.NewGuid().ToString()), TestContext.Current.CancellationToken);
        var candidate = await service.RegisterCandidateAsync(new(exam.Id, UserId.New(), "A", Guid.NewGuid().ToString()), TestContext.Current.CancellationToken);
        await service.OpenExamAsync(exam.Id, TestContext.Current.CancellationToken);
        var session = await service.StartExamSessionAsync(new(exam.Id, room.Id, candidate.Id, Guid.NewGuid().ToString()), TestContext.Current.CancellationToken);
        return (service, session);
    }

    private sealed class TestRuntime : IAggregateStore, IUnitOfWork, IClock, IAuthorizationService,
        ISourceRevisionStorage, IDurableJudgeQueue, ITransactionalOutbox
    {
        public DateTimeOffset Now { get; } = new(2026, 7, 22, 1, 2, 3, TimeSpan.Zero);
        public DateTimeOffset UtcNow => Now;
        public Dictionary<ExamId, Exam> Exams { get; } = [];
        public Dictionary<RoomId, ExamRoom> Rooms { get; } = [];
        public Dictionary<SessionId, ExamSession> Sessions { get; } = [];
        public Dictionary<SubmissionId, Submission> Submissions { get; } = [];
        public List<JudgeJob> Jobs { get; } = [];
        public List<object> Outbox { get; } = [];
        private readonly Dictionary<ViolationEventId, ViolationEvent> _events = [];
        private readonly Dictionary<ViolationEvaluationId, ViolationEvaluation> _evaluations = [];
        private readonly HashSet<string> _keys = [];

        public MastemisService CreateService() => new(this, this, this, this, this, this, this);
        public ValueTask EnsureAsync(string permission, Guid scopeId, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return ValueTask.CompletedTask; }
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct) => operation(ct);
        public Task AddExamAsync(Exam value, CancellationToken ct) { Exams.Add(value.Id, value); return Task.CompletedTask; }
        public Task<Exam?> GetExamAsync(ExamId id, CancellationToken ct) => Task.FromResult(Exams.GetValueOrDefault(id));
        public Task AddRoomAsync(ExamRoom value, CancellationToken ct) { Rooms.Add(value.Id, value); return Task.CompletedTask; }
        public Task<ExamRoom?> GetRoomAsync(RoomId id, CancellationToken ct) => Task.FromResult(Rooms.GetValueOrDefault(id));
        public Task AddCandidateAsync(Candidate value, ExamId examId, CancellationToken ct) => Task.CompletedTask;
        public Task AddSessionAsync(ExamSession value, CancellationToken ct) { Sessions.Add(value.Id, value); return Task.CompletedTask; }
        public Task<ExamSession?> GetSessionAsync(SessionId id, CancellationToken ct) => Task.FromResult(Sessions.GetValueOrDefault(id));
        public Task AddRevisionAsync(SourceRevisionMetadata value, CancellationToken ct) => Task.CompletedTask;
        public Task AddSubmissionAsync(Submission value, CancellationToken ct) { Submissions.Add(value.Id, value); return Task.CompletedTask; }
        public Task<Submission?> GetSubmissionAsync(SubmissionId id, CancellationToken ct) => Task.FromResult(Submissions.GetValueOrDefault(id));
        public Task<IReadOnlyList<Submission>> GetSubmissionsAsync(SessionId id, CancellationToken ct) => Task.FromResult<IReadOnlyList<Submission>>(Submissions.Values.Where(x => x.SessionId == id).ToArray());
        public Task AddEventAsync(ViolationEvent value, CancellationToken ct) { _events.Add(value.Id, value); return Task.CompletedTask; }
        public Task<ViolationEvent?> GetEventAsync(ViolationEventId id, CancellationToken ct) => Task.FromResult(_events.GetValueOrDefault(id));
        public Task AddEvaluationAsync(ViolationEvaluation value, CancellationToken ct) { _evaluations.Add(value.Id, value); return Task.CompletedTask; }
        public Task<ViolationEvaluation?> GetEvaluationAsync(ViolationEvaluationId id, CancellationToken ct) => Task.FromResult(_evaluations.GetValueOrDefault(id));
        public Task AddWarningAsync(ConfirmedWarning value, CancellationToken ct) => Task.CompletedTask;
        public Task AddJudgeJobAsync(JudgeJob value, CancellationToken ct) => Task.CompletedTask;
        public Task AddTerminationMetadataAsync(TerminationMetadata value, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> HasIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(_keys.Contains(key));
        public Task AddIdempotencyKeyAsync(string key, CancellationToken ct) { _keys.Add(key); return Task.CompletedTask; }
        public Task<ExamSummary> GetExamSummaryAsync(ExamId id, CancellationToken ct) => Task.FromResult(new ExamSummary(id, 0, 0, 0, 0, 0));
        public Task<StoredSourceRevision> StoreAsync(SourceRevisionId id, ReadOnlyMemory<byte> content, CancellationToken ct) => Task.FromResult(new StoredSourceRevision($"source/{id.Value:N}", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content.Span)), content.Length));
        public Task EnqueueAsync(JudgeJob job, CancellationToken ct) { Jobs.Add(job); return Task.CompletedTask; }
        public Task<JudgeJob?> ClaimAsync(JudgeWorkerId id, TimeSpan lease, CancellationToken ct) => Task.FromResult<JudgeJob?>(null);
        public Task CompleteAsync(JudgeJobId id, Judgement judgement, CancellationToken ct) => Task.CompletedTask;
        public Task AddAsync<T>(T message, CancellationToken ct) where T : notnull { Outbox.Add(message); return Task.CompletedTask; }
    }
}
