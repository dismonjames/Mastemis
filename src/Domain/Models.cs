namespace Mastemis.Domain;

public sealed record User(UserId Id, string DisplayName);
public sealed record Candidate(CandidateId Id, UserId UserId, string RegistrationCode);
public sealed record ExamRoom(RoomId Id, ExamId ExamId, string Name);
public sealed record Problem(ProblemId Id, string Title, int TimeLimitMilliseconds, int MemoryLimitMegabytes);
public sealed record SourceRevisionMetadata(SourceRevisionId Id, SessionId SessionId, string ObjectId, string Sha256,
    DateTimeOffset CreatedAtUtc, long Length = 0);

public sealed record Submission
{
    public Submission(SubmissionId id, SessionId sessionId, ProblemId problemId, SourceRevisionId revisionId,
        string language, DateTimeOffset createdAtUtc, bool isFinal = false)
    {
        if (string.IsNullOrWhiteSpace(language)) throw new DomainException("submission.language_required", "A language is required.");
        Id = id; SessionId = sessionId; ProblemId = problemId; RevisionId = revisionId;
        Language = language; CreatedAtUtc = Exam.EnsureUtc(createdAtUtc); IsFinal = isFinal;
    }
    public SubmissionId Id { get; }
    public SessionId SessionId { get; }
    public ProblemId ProblemId { get; }
    public SourceRevisionId RevisionId { get; }
    public string Language { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public bool IsFinal { get; }
    public SubmissionState State { get; init; } = SubmissionState.Pending;
}

public sealed record ViolationEvent(ViolationEventId Id, SessionId SessionId, long ClientSequence, DateTimeOffset ClientTimestamp,
    DateTimeOffset ServerReceivedAtUtc, string EventType, IReadOnlyDictionary<string, string> Metadata);
public sealed record ViolationEvaluation(ViolationEvaluationId Id, ViolationEventId EventId, SessionId SessionId,
    EvaluationResult Result, string PolicyCode, DateTimeOffset EvaluatedAtUtc);
public sealed record ConfirmedWarning(WarningId Id, SessionId SessionId, ViolationEvaluationId EvaluationId,
    int Ordinal, DateTimeOffset IssuedAtUtc);
public sealed record Judgement(SubmissionId SubmissionId, SubmissionState Verdict, int Score, DateTimeOffset CompletedAtUtc);
public sealed record JudgeJob(JudgeJobId Id, SubmissionId SubmissionId, JudgeJobState State, int Attempt,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? LeaseExpiresAtUtc = null, JudgeWorkerId? WorkerId = null);
public sealed record JudgeWorker(JudgeWorkerId Id, string Name, int Capacity, DateTimeOffset LastHeartbeatUtc);
public sealed record EvidencePackageMetadata(EvidencePackageId Id, ExamId ExamId, RoomId RoomId, CandidateId CandidateId,
    SessionId SessionId, DateTimeOffset CreatedAtUtc, string? LatestChainHash);
