using Mastemis.Domain;

namespace Mastemis.Application;

public interface IClock { DateTimeOffset UtcNow { get; } }
public interface ICurrentUser { UserId? UserId { get; } }
public interface IAuthorizationService
{
    ValueTask EnsureAsync(string permission, Guid scopeId, CancellationToken cancellationToken);
}
public interface IUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
public interface IAggregateStore
{
    Task AddExamAsync(Exam exam, CancellationToken cancellationToken);
    Task<Exam?> GetExamAsync(ExamId id, CancellationToken cancellationToken);
    Task AddRoomAsync(ExamRoom room, CancellationToken cancellationToken);
    Task<ExamRoom?> GetRoomAsync(RoomId id, CancellationToken cancellationToken);
    Task AddCandidateAsync(Candidate candidate, ExamId examId, CancellationToken cancellationToken);
    Task AddSessionAsync(ExamSession session, CancellationToken cancellationToken);
    Task<ExamSession?> GetSessionAsync(SessionId id, CancellationToken cancellationToken);
    Task AddRevisionAsync(SourceRevisionMetadata revision, CancellationToken cancellationToken);
    Task AddSubmissionAsync(Submission submission, CancellationToken cancellationToken);
    Task<Submission?> GetSubmissionAsync(SubmissionId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Submission>> GetSubmissionsAsync(SessionId sessionId, CancellationToken cancellationToken);
    Task AddEventAsync(ViolationEvent activityEvent, CancellationToken cancellationToken);
    Task<ViolationEvent?> GetEventAsync(ViolationEventId id, CancellationToken cancellationToken);
    Task AddEvaluationAsync(ViolationEvaluation evaluation, CancellationToken cancellationToken);
    Task<ViolationEvaluation?> GetEvaluationAsync(ViolationEvaluationId id, CancellationToken cancellationToken);
    Task AddWarningAsync(ConfirmedWarning warning, CancellationToken cancellationToken);
    Task AddJudgeJobAsync(JudgeJob job, CancellationToken cancellationToken);
    Task AddTerminationMetadataAsync(TerminationMetadata metadata, CancellationToken cancellationToken);
    Task<bool> HasIdempotencyKeyAsync(string key, CancellationToken cancellationToken);
    Task AddIdempotencyKeyAsync(string key, CancellationToken cancellationToken);
    Task<ExamSummary> GetExamSummaryAsync(ExamId examId, CancellationToken cancellationToken);
}
public interface ISourceRevisionStorage
{
    Task<StoredSourceRevision> StoreAsync(SourceRevisionId id, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
}
public sealed record StoredSourceRevision(string ObjectId, string Sha256, long Length);
public interface IObjectStorage
{
    Task<string> PutAsync(string category, string objectId, Stream content, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string objectId, CancellationToken cancellationToken);
}
public interface IDurableJudgeQueue
{
    Task EnqueueAsync(JudgeJob job, CancellationToken cancellationToken);
    Task<JudgeJob?> ClaimAsync(JudgeWorkerId workerId, TimeSpan lease, CancellationToken cancellationToken);
    Task CompleteAsync(JudgeJobId jobId, Judgement judgement, CancellationToken cancellationToken);
}
public interface IWorkerJudgeQueue
{
    Task<WorkerJobLease?> ClaimAsync(JudgeWorkerId workerId, TimeSpan leaseDuration, CancellationToken cancellationToken);
    Task RenewAsync(JudgeWorkerId workerId, JudgeJobId jobId, Guid leaseId, TimeSpan leaseDuration, CancellationToken cancellationToken);
    Task StartAsync(JudgeWorkerId workerId, JudgeJobId jobId, Guid leaseId, CancellationToken cancellationToken);
    Task CompleteAsync(JudgeWorkerId workerId, JudgeJobId jobId, Guid leaseId, Judgement judgement, CancellationToken cancellationToken);
    Task CompleteDetailedAsync(JudgeWorkerId workerId, JudgeJobId jobId, Guid leaseId,
        WorkerJudgementCompletion completion, CancellationToken cancellationToken) =>
        CompleteAsync(workerId, jobId, leaseId, completion.Judgement, cancellationToken);
    Task FailAsync(JudgeWorkerId workerId, JudgeJobId jobId, Guid leaseId, string failureCode, CancellationToken cancellationToken);
}
public sealed record WorkerJudgementCompletion(Judgement Judgement, int? FailedTestIndex, long ExecutionMilliseconds,
    long? PeakMemoryBytes, int? ExitCode, int? Signal, long StandardOutputBytes, long StandardErrorBytes,
    string? CompilerDiagnosticSummary, string? RuntimeDiagnosticSummary, string? CheckerDiagnosticSummary,
    string SandboxBackend, JudgeWorkerId WorkerId, string JudgeVersion);
public sealed record WorkerJobLease(JudgeJobId JobId, SubmissionId SubmissionId, Guid LeaseId,
    DateTimeOffset LeaseExpiresAtUtc, int Attempt, int MaximumAttempts);
public interface IWorkerCredentialService
{
    Task<IssuedWorkerCredential> RegisterAsync(string name, int capacity, DateTimeOffset? expiresAtUtc, CancellationToken cancellationToken);
    Task<IssuedWorkerCredential> RotateAsync(JudgeWorkerId workerId, DateTimeOffset? expiresAtUtc, CancellationToken cancellationToken);
    Task RevokeAsync(JudgeWorkerId workerId, CancellationToken cancellationToken);
    Task<bool> AuthenticateAsync(JudgeWorkerId workerId, string secret, CancellationToken cancellationToken);
    Task HeartbeatAsync(JudgeWorkerId workerId, int capacity, CancellationToken cancellationToken);
}
public sealed record IssuedWorkerCredential(JudgeWorkerId WorkerId, string Secret, DateTimeOffset? ExpiresAtUtc);
public interface IEventPublisher { Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : notnull; }
public interface ITransactionalOutbox { Task AddAsync<T>(T message, CancellationToken cancellationToken) where T : notnull; }
public interface IOutboxPublisher { Task PublishAsync(string messageId, string messageType, string payload, CancellationToken cancellationToken); }
public interface IHashingService { string Sha256(ReadOnlySpan<byte> content); }
public interface IPackageStorage { Task<string> StoreAsync(Stream package, CancellationToken cancellationToken); }
public interface IEvidenceIngestion { Task AddAsync(EvidencePackageId packageId, EvidenceItemType type, Stream content, CancellationToken cancellationToken); }

public sealed record ExamSummary(ExamId ExamId, int ActiveSessions, int DisconnectedSessions, int WarningCount,
    int TerminatedSessions, int QueuedSubmissions);
public sealed record TerminationMetadata(SessionId SessionId, WarningId WarningId, SourceRevisionId FrozenRevisionId,
    SubmissionId FinalSubmissionId, JudgeJobId JudgeJobId, DateTimeOffset CreatedAtUtc);
