namespace Mastemis.Domain;

public interface IStrongId { Guid Value { get; } }

public readonly record struct UserId(Guid Value) : IStrongId { public static UserId New() => new(Guid.NewGuid()); }
public readonly record struct ExamId(Guid Value) : IStrongId { public static ExamId New() => new(Guid.NewGuid()); }
public readonly record struct RoomId(Guid Value) : IStrongId { public static RoomId New() => new(Guid.NewGuid()); }
public readonly record struct CandidateId(Guid Value) : IStrongId { public static CandidateId New() => new(Guid.NewGuid()); }
public readonly record struct SessionId(Guid Value) : IStrongId { public static SessionId New() => new(Guid.NewGuid()); }
public readonly record struct ProblemId(Guid Value) : IStrongId { public static ProblemId New() => new(Guid.NewGuid()); }
public readonly record struct SubmissionId(Guid Value) : IStrongId { public static SubmissionId New() => new(Guid.NewGuid()); }
public readonly record struct WarningId(Guid Value) : IStrongId { public static WarningId New() => new(Guid.NewGuid()); }
public readonly record struct ViolationEventId(Guid Value) : IStrongId { public static ViolationEventId New() => new(Guid.NewGuid()); }
public readonly record struct ViolationEvaluationId(Guid Value) : IStrongId { public static ViolationEvaluationId New() => new(Guid.NewGuid()); }
public readonly record struct EvidencePackageId(Guid Value) : IStrongId { public static EvidencePackageId New() => new(Guid.NewGuid()); }
public readonly record struct SourceRevisionId(Guid Value) : IStrongId { public static SourceRevisionId New() => new(Guid.NewGuid()); }
public readonly record struct JudgeJobId(Guid Value) : IStrongId { public static JudgeJobId New() => new(Guid.NewGuid()); }
public readonly record struct JudgeWorkerId(Guid Value) : IStrongId { public static JudgeWorkerId New() => new(Guid.NewGuid()); }
