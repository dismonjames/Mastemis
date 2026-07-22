namespace Mastemis.Contracts.Judge;

public sealed record CompilationRequest(
    IReadOnlyList<SourceFile> Sources,
    string SourceDirectory,
    string BuildDirectory,
    ResourceLimits Limits);

public sealed record CompiledArtifact(string ExecutablePath, IReadOnlyList<string> Arguments, string ArtifactType);

public sealed record CompilationResult(
    bool Succeeded,
    CompiledArtifact? Artifact,
    IReadOnlyList<JudgeDiagnostic> Diagnostics,
    JudgeFailureCode? FailureCode,
    TimeSpan Duration,
    long OutputBytes);

public sealed record ExecutionPlan(string Executable, IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment);

public sealed record TestExecutionRequest(TestCaseDescriptor TestCase, CompiledArtifact Artifact, ResourceLimits Limits);
public sealed record TestExecutionResult(int TestIndex, int? ExitCode, int? Signal, TimeSpan WallTime,
    TimeSpan CpuTime, long? PeakMemoryBytes, long StandardOutputBytes, long StandardErrorBytes,
    JudgeFailureCode? FailureCode, string SandboxBackend);
