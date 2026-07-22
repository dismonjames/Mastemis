using System.Collections.Concurrent;
using Mastemis.Application;
using Mastemis.Domain;

namespace Mastemis.Infrastructure;

public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
public sealed class DevelopmentAuthorizationService : IAuthorizationService
{
    public ValueTask EnsureAsync(string permission, Guid scopeId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}

public sealed class UnconfiguredAuthorizationService : IAuthorizationService
{
    public ValueTask EnsureAsync(string permission, Guid scopeId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new ApplicationFailure(ErrorCodes.Forbidden, "Identity and authorization must be configured for management operations.");
    }
}

public sealed class InMemoryRuntime : IAggregateStore, IUnitOfWork, IDurableJudgeQueue, ITransactionalOutbox
{
    private readonly SemaphoreSlim _transaction = new(1, 1);
    private readonly ConcurrentDictionary<ExamId, Exam> _exams = new();
    private readonly ConcurrentDictionary<RoomId, ExamRoom> _rooms = new();
    private readonly ConcurrentDictionary<CandidateId, (Candidate Candidate, ExamId ExamId)> _candidates = new();
    private readonly ConcurrentDictionary<SessionId, ExamSession> _sessions = new();
    private readonly ConcurrentDictionary<SourceRevisionId, SourceRevisionMetadata> _revisions = new();
    private readonly ConcurrentDictionary<SubmissionId, Submission> _submissions = new();
    private readonly ConcurrentDictionary<ViolationEventId, ViolationEvent> _events = new();
    private readonly ConcurrentDictionary<ViolationEvaluationId, ViolationEvaluation> _evaluations = new();
    private readonly ConcurrentDictionary<WarningId, ConfirmedWarning> _warnings = new();
    private readonly ConcurrentDictionary<JudgeJobId, JudgeJob> _jobs = new();
    private readonly ConcurrentDictionary<SessionId, TerminationMetadata> _terminations = new();
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<object> _outbox = new();

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await _transaction.WaitAsync(cancellationToken);
        try { return await operation(cancellationToken); }
        finally { _transaction.Release(); }
    }

    public Task AddExamAsync(Exam exam, CancellationToken ct) => AddAsync(_exams, exam.Id, exam, ct);
    public Task<Exam?> GetExamAsync(ExamId id, CancellationToken ct) => GetAsync(_exams, id, ct);
    public Task AddRoomAsync(ExamRoom room, CancellationToken ct) => AddAsync(_rooms, room.Id, room, ct);
    public Task<ExamRoom?> GetRoomAsync(RoomId id, CancellationToken ct) => GetAsync(_rooms, id, ct);
    public Task AddCandidateAsync(Candidate candidate, ExamId examId, CancellationToken ct) => AddAsync(_candidates, candidate.Id, (candidate, examId), ct);
    public Task AddSessionAsync(ExamSession session, CancellationToken ct) => AddAsync(_sessions, session.Id, session, ct);
    public Task<ExamSession?> GetSessionAsync(SessionId id, CancellationToken ct) => GetAsync(_sessions, id, ct);
    public Task AddRevisionAsync(SourceRevisionMetadata revision, CancellationToken ct) => AddAsync(_revisions, revision.Id, revision, ct);
    public Task AddSubmissionAsync(Submission submission, CancellationToken ct) => AddAsync(_submissions, submission.Id, submission, ct);
    public Task<Submission?> GetSubmissionAsync(SubmissionId id, CancellationToken ct) => GetAsync(_submissions, id, ct);
    public Task AddEventAsync(ViolationEvent activityEvent, CancellationToken ct) => AddAsync(_events, activityEvent.Id, activityEvent, ct);
    public Task<ViolationEvent?> GetEventAsync(ViolationEventId id, CancellationToken ct) => GetAsync(_events, id, ct);
    public Task AddEvaluationAsync(ViolationEvaluation evaluation, CancellationToken ct) => AddAsync(_evaluations, evaluation.Id, evaluation, ct);
    public Task<ViolationEvaluation?> GetEvaluationAsync(ViolationEvaluationId id, CancellationToken ct) => GetAsync(_evaluations, id, ct);
    public Task AddWarningAsync(ConfirmedWarning warning, CancellationToken ct) => AddAsync(_warnings, warning.Id, warning, ct);
    public Task AddJudgeJobAsync(JudgeJob job, CancellationToken ct) => AddAsync(_jobs, job.Id, job, ct);
    public Task AddTerminationMetadataAsync(TerminationMetadata metadata, CancellationToken ct) => AddAsync(_terminations, metadata.SessionId, metadata, ct);

    public Task<IReadOnlyList<Submission>> GetSubmissionsAsync(SessionId sessionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<Submission> result = _submissions.Values.Where(x => x.SessionId == sessionId).OrderByDescending(x => x.CreatedAtUtc).ToArray();
        return Task.FromResult(result);
    }

    public Task<bool> HasIdempotencyKeyAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_keys.ContainsKey(key));
    }

    public Task AddIdempotencyKeyAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!_keys.TryAdd(key, 0)) throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "The idempotency key was already used.");
        return Task.CompletedTask;
    }

    public Task<ExamSummary> GetExamSummaryAsync(ExamId examId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var sessions = _sessions.Values.Where(x => x.ExamId == examId).ToArray();
        var ids = sessions.Select(x => x.Id).ToHashSet();
        var summary = new ExamSummary(examId,
            sessions.Count(x => x.State == SessionState.Active),
            sessions.Count(x => x.State == SessionState.Disconnected),
            sessions.Sum(x => x.Warnings.Count),
            sessions.Count(x => x.State == SessionState.Terminated),
            _submissions.Values.Count(x => ids.Contains(x.SessionId) && x.State is SubmissionState.Pending or SubmissionState.Queued));
        return Task.FromResult(summary);
    }

    public Task EnqueueAsync(JudgeJob job, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _jobs.TryAdd(job.Id, job);
        return Task.CompletedTask;
    }

    public Task<JudgeJob?> ClaimAsync(JudgeWorkerId workerId, TimeSpan lease, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _jobs.OrderBy(x => x.Value.CreatedAtUtc))
        {
            var job = pair.Value;
            if (job.State != JudgeJobState.Pending && !(job.State == JudgeJobState.Claimed && job.LeaseExpiresAtUtc <= now)) continue;
            var claimed = job with { State = JudgeJobState.Claimed, WorkerId = workerId, LeaseExpiresAtUtc = now + lease, Attempt = job.Attempt + 1 };
            if (_jobs.TryUpdate(pair.Key, claimed, job)) return Task.FromResult<JudgeJob?>(claimed);
        }
        return Task.FromResult<JudgeJob?>(null);
    }

    public Task CompleteAsync(JudgeJobId jobId, Judgement judgement, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!_jobs.TryGetValue(jobId, out var job)) throw new ApplicationFailure(ErrorCodes.NotFound, "Judge job not found.");
        if (job.SubmissionId != judgement.SubmissionId) throw new ApplicationFailure(ErrorCodes.InvalidInput, "Judgement belongs to another submission.");
        _jobs[jobId] = job with { State = JudgeJobState.Completed, LeaseExpiresAtUtc = null };
        return Task.CompletedTask;
    }

    public Task AddAsync<T>(T message, CancellationToken ct) where T : notnull
    {
        ct.ThrowIfCancellationRequested();
        _outbox.Enqueue(message);
        return Task.CompletedTask;
    }

    private static Task AddAsync<TKey, TValue>(ConcurrentDictionary<TKey, TValue> dictionary, TKey key, TValue value, CancellationToken ct) where TKey : notnull
    {
        ct.ThrowIfCancellationRequested();
        if (!dictionary.TryAdd(key, value)) throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "The resource already exists.");
        return Task.CompletedTask;
    }

    private static Task<TValue?> GetAsync<TKey, TValue>(ConcurrentDictionary<TKey, TValue> dictionary, TKey key, CancellationToken ct) where TKey : notnull where TValue : class
    {
        ct.ThrowIfCancellationRequested();
        dictionary.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }
}
