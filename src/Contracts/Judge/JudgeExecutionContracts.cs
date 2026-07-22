using Mastemis.Domain;

namespace Mastemis.Contracts.Judge;

public sealed record ResourceLimits(
    TimeSpan CpuTime,
    TimeSpan WallTime,
    long MemoryBytes,
    long OutputBytes,
    long FileBytes,
    int ProcessCount,
    int TestCount,
    TimeSpan CompilationTime,
    long CompilationOutputBytes)
{
    public void Validate()
    {
        if (CpuTime <= TimeSpan.Zero || WallTime < CpuTime || WallTime > TimeSpan.FromMinutes(10)) Invalid("time");
        if (MemoryBytes is < 16_777_216 or > 17_179_869_184) Invalid("memory");
        if (OutputBytes is < 1 or > 67_108_864 || FileBytes < OutputBytes || FileBytes > 1_073_741_824) Invalid("output");
        if (ProcessCount is < 1 or > 128 || TestCount is < 1 or > 10_000) Invalid("count");
        if (CompilationTime <= TimeSpan.Zero || CompilationTime > TimeSpan.FromMinutes(10) || CompilationOutputBytes is < 1 or > 16_777_216) Invalid("compilation");
    }

    private static void Invalid(string field) => throw new JudgeContractException(JudgeFailureCode.InvalidContract, $"Invalid {field} limits.");
}

public sealed record SourceFile(string FileName, ReadOnlyMemory<byte> Content);
public sealed record TestCaseDescriptor(int Index, ReadOnlyMemory<byte> Input, ReadOnlyMemory<byte> ExpectedOutput, string CheckerId);
public sealed record RuntimeEnvironment(string Architecture, IReadOnlyDictionary<string, string> Variables);

public sealed record JudgeExecutionRequest(
    JudgeJobId JobId,
    SubmissionId SubmissionId,
    JudgeWorkerId WorkerId,
    string LanguageId,
    IReadOnlyList<SourceFile> Sources,
    IReadOnlyList<TestCaseDescriptor> Tests,
    ResourceLimits Limits,
    RuntimeEnvironment Environment,
    bool FailFast = true)
{
    public void Validate()
    {
        Limits.Validate();
        if (string.IsNullOrWhiteSpace(LanguageId) || LanguageId.Length > 32) Invalid("language");
        if (Sources.Count is < 1 or > 32 || Tests.Count is < 1 || Tests.Count > Limits.TestCount) Invalid("collection");
        if (Tests.Select(x => x.Index).Distinct().Count() != Tests.Count || Tests.Any(x => x.Index < 1)) Invalid("test order");
        if (Environment.Variables.Count > 32 || Environment.Variables.Any(x => x.Key.Length > 100 || x.Value.Length > 1000)) Invalid("environment");
    }
    private static void Invalid(string field) => throw new JudgeContractException(JudgeFailureCode.InvalidContract, $"Invalid {field} contract.");
}

public sealed record JudgeExecutionResult(
    SubmissionState Verdict,
    TimeSpan ExecutionTime,
    long? PeakMemoryBytes,
    int? ExitCode,
    int? Signal,
    long StandardOutputBytes,
    long StandardErrorBytes,
    IReadOnlyList<JudgeDiagnostic> CompilerDiagnostics,
    string? InfrastructureFailureReason,
    int? FailedTestIndex,
    JudgeWorkerId WorkerId,
    string JudgeVersion,
    string SandboxBackend,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);
