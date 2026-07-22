using Mastemis.Domain;

namespace Mastemis.Contracts.Judge;

public sealed record WorkerLeaseContract(JudgeJobId JobId, SubmissionId SubmissionId, Guid LeaseId,
    DateTimeOffset LeaseExpiresAtUtc, int Attempt, int MaximumAttempts);

public sealed record WorkerTestContract(int Index, string CheckerId, long InputBytes, long ExpectedOutputBytes);

public sealed record WorkerJudgeContract(JudgeJobId JobId, SubmissionId SubmissionId, string LanguageId,
    ResourceLimits Limits, IReadOnlyList<WorkerTestContract> Tests);

public sealed record WorkerJudgementReport(Guid LeaseId, SubmissionId SubmissionId, JudgeExecutionResult Result);
public sealed record WorkerFailureReport(Guid LeaseId, string FailureCode);
public sealed record WorkerLeaseRenewal(Guid LeaseId, int LeaseSeconds);
public sealed record WorkerHeartbeatContract(int Capacity, IReadOnlyList<string> Languages, string SandboxBackend);
