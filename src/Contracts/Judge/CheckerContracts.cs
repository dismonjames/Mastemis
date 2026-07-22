namespace Mastemis.Contracts.Judge;

public sealed record CheckerRequest(ReadOnlyMemory<byte> Expected, ReadOnlyMemory<byte> Actual, long MaximumBytes);
public sealed record CheckerResult(bool Accepted, JudgeDiagnostic? Diagnostic);

public sealed record JudgeDiagnostic(string Code, string Message, JudgeDiagnosticSeverity Severity);
public enum JudgeDiagnosticSeverity { Information, Warning, Error }
public enum JudgeFailureCode
{
    InvalidContract,
    UnsupportedLanguage,
    UnsafeSourceName,
    CompilerNotFound,
    CompilationFailed,
    CompilationTimedOut,
    CompilationOutputLimit,
    ArtifactMissing,
    SandboxUnavailable,
    SandboxFailure,
    RuntimeFailure,
    TimeLimit,
    MemoryLimit,
    OutputLimit,
    ProcessLimit,
    FileLimit,
    CheckerFailure,
    Cancelled,
    LeaseLost,
    ServerUnavailable
}

public sealed class JudgeContractException(JudgeFailureCode code, string message) : Exception(message)
{
    public JudgeFailureCode Code { get; } = code;
}
