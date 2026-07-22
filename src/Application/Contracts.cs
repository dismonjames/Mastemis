using Mastemis.Domain;

namespace Mastemis.Application;

public sealed record CreateExamCommand(string Title, string IdempotencyKey);
public sealed record ScheduleExamCommand(ExamId ExamId, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, string IdempotencyKey);
public sealed record CreateRoomCommand(ExamId ExamId, string Name, string IdempotencyKey);
public sealed record RegisterCandidateCommand(ExamId ExamId, UserId UserId, string RegistrationCode, string IdempotencyKey);
public sealed record StartSessionCommand(ExamId ExamId, RoomId RoomId, CandidateId CandidateId, string IdempotencyKey);
public sealed record SaveDraftCommand(SessionId SessionId, ReadOnlyMemory<byte> Content, string IdempotencyKey);
public sealed record CreateSubmissionCommand(SessionId SessionId, ProblemId ProblemId, SourceRevisionId RevisionId,
    string Language, string IdempotencyKey);
public sealed record RecordSfeEventCommand(SessionId SessionId, long ClientSequence, DateTimeOffset ClientTimestamp,
    string EventType, IReadOnlyDictionary<string, string> Metadata, string IdempotencyKey);
public sealed record EvaluateSfeEventCommand(ViolationEventId EventId, TimeSpan? Duration, bool ConcurrentSession);
public sealed record ConfirmViolationCommand(SessionId SessionId, ViolationEvaluationId EvaluationId,
    SourceRevisionId AuthoritativeRevisionId, ProblemId ProblemId, string Language, string IdempotencyKey);
public sealed record IssueStoredWarningCommand(SessionId SessionId, ViolationEvaluationId EvaluationId,
    ProblemId ProblemId, string Language, string IdempotencyKey);

public sealed record SessionTerminated(SessionId SessionId, WarningId WarningId, SourceRevisionId FrozenRevisionId,
    SubmissionId FinalSubmissionId, JudgeJobId JudgeJobId, DateTimeOffset TerminatedAtUtc);
public sealed record WarningIssued(SessionId SessionId, WarningId WarningId, int Ordinal);
public sealed record DraftSaved(SessionId SessionId, SourceRevisionId RevisionId);
public sealed record SubmissionCreated(SessionId SessionId, SubmissionId SubmissionId, bool IsFinal);
public sealed record RealtimeEnvelope(string MessageId, int Version, string Type, DateTimeOffset OccurredAtUtc, string Payload);
public sealed record CandidateConnected(SessionId SessionId, CandidateId CandidateId);
public sealed record CandidateDisconnected(SessionId SessionId, CandidateId CandidateId);
public sealed record JudgementUpdated(SubmissionId SubmissionId, SubmissionState Verdict);
public sealed record SfeEventReceived(SessionId SessionId, ViolationEventId EventId);
public sealed record SfeEvaluationCreated(SessionId SessionId, ViolationEvaluationId EvaluationId, EvaluationResult Result);
public sealed record WorkerConnected(JudgeWorkerId WorkerId);
public sealed record WorkerDisconnected(JudgeWorkerId WorkerId);
public sealed record WorkerCapacityChanged(JudgeWorkerId WorkerId, int Capacity);
public sealed record JudgeJobQueued(JudgeJobId JobId, SubmissionId SubmissionId);
public sealed record JudgeJobClaimed(JudgeJobId JobId, JudgeWorkerId WorkerId);
public sealed record JudgeJobCompleted(JudgeJobId JobId, SubmissionId SubmissionId);
