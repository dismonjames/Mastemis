namespace Mastemis.Domain;

public enum ExamState { Draft, Scheduled, Open, Closed, Cancelled }
public enum SessionState { Pending, Active, Disconnected, Completed, Terminated }
public enum SubmissionState
{
    Pending, Queued, Compiling, Running, Accepted, WrongAnswer, CompilationError, RuntimeError,
    TimeLimitExceeded, MemoryLimitExceeded, OutputLimitExceeded, InfrastructureError, Cancelled
}
public enum EvaluationResult { Ignored, Recorded, Suspected, ConfirmedViolation }
public enum JudgeJobState { Pending, Claimed, Running, Completed, Failed, Cancelled }
public enum EvidenceItemType
{
    RawEvent, NormalizedEvent, PolicyEvaluation, Warning, SourceRevision, Screenshot,
    ConnectionRecord, SessionTransition, FinalJudgement, InvigilatorNote, AccessAudit
}
public enum CandidateExamAccessState { Enabled, Disabled, Terminated }
