using Mastemis.Contracts.Judge;
using Mastemis.Domain;

namespace Mastemis.Contracts.Problems.ReferenceOutputs;

public enum ReferenceOutputJobStatus { Pending, Claimed, Running, Completed, Failed, Cancelled }
public enum ReferenceOutputResultStatus { Completed, RuntimeError, TimeLimitExceeded, MemoryLimitExceeded, OutputLimitExceeded, InfrastructureError }

public sealed record ReferenceSolutionSource(string FileName, string ObjectId, string Sha256, long Length);
public sealed record ReferenceOutputTestCase(int TestIndex, string InputObjectId, string InputSha256, long InputLength);

public sealed record ReferenceOutputJobPayload(int ContractVersion, Guid JobId, Guid OperationId, ProblemId ProblemId,
    int DraftVersion, Guid ReferenceSolutionRevisionId, string Language, IReadOnlyList<ReferenceSolutionSource> Sources,
    IReadOnlyList<ReferenceOutputTestCase> Tests, ResourceLimits Limits, TimeSpan TotalTimeLimit)
{
    public const int CurrentVersion = 1;
    public void Validate()
    {
        if (ContractVersion != CurrentVersion || JobId == Guid.Empty || OperationId == Guid.Empty || ProblemId.Value == Guid.Empty ||
            ReferenceSolutionRevisionId == Guid.Empty || Language is not ("cpp" or "csharp") || DraftVersion < 1 ||
            Sources.Count is < 1 or > 32 || Tests.Count is < 1 or > 10_000 || TotalTimeLimit <= TimeSpan.Zero || TotalTimeLimit > TimeSpan.FromHours(1)) Invalid();
        if (Sources.Any(x => string.IsNullOrWhiteSpace(x.ObjectId) || x.Sha256.Length != 64 || x.Length is < 1 or > 4_194_304) ||
            Tests.Any(x => x.TestIndex < 1 || string.IsNullOrWhiteSpace(x.InputObjectId) || x.InputSha256.Length != 64 || x.InputLength is < 0 or > 67_108_864) ||
            Tests.Select(x => x.TestIndex).Distinct().Count() != Tests.Count) Invalid();
        Limits.Validate();
    }
    private static void Invalid() => throw new JudgeContractException(JudgeFailureCode.InvalidContract, "Reference output job contract is invalid.");
}

public sealed record ReferenceOutputJobLease(Guid JobId, Guid OperationId, JudgeWorkerId WorkerId, Guid LeaseToken,
    DateTimeOffset LeaseExpiresAtUtc, int Attempt, int MaximumAttempts);
public sealed record ReferenceOutputTestResult(int ContractVersion, Guid JobId, Guid OperationId, JudgeWorkerId WorkerId,
    Guid LeaseToken, int TestIndex, string OutputObjectId, string OutputSha256, long OutputLength,
    long ExecutionMilliseconds, long? PeakMemoryBytes, string SandboxBackend, string JudgeVersion,
    ReferenceOutputResultStatus Status, string? FailureCode);
public sealed record ReferenceOutputProgress(Guid JobId, Guid OperationId, int CompletedTests, int TotalTests);
public sealed record ReferenceOutputCompletion(Guid JobId, Guid OperationId, JudgeWorkerId WorkerId, Guid LeaseToken,
    int CompletedTests, string JudgeVersion, string SandboxBackend);
public sealed record ReferenceOutputFailure(Guid JobId, Guid OperationId, JudgeWorkerId WorkerId, Guid LeaseToken,
    string FailureCode, string? DiagnosticSummary);
