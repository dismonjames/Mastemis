namespace Mastemis.Contracts.Problems.ReferenceValidation;

public enum ReferenceValidationStatus
{
    Pending,
    Claimed,
    Running,
    Succeeded,
    Failed,
    CancelRequested,
    Cancelled,
    Expired
}

public enum ReferenceDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public enum ReferenceValidationExitClassification
{
    Compiled,
    CompilationError,
    TimedOut,
    Cancelled,
    InfrastructureError
}
