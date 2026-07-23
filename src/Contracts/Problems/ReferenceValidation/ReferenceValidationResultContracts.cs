using Mastemis.Contracts.Judge;
using Mastemis.Domain;

namespace Mastemis.Contracts.Problems.ReferenceValidation;

public sealed record ReferenceCompilerDiagnostic(
    ReferenceDiagnosticSeverity Severity,
    string? Code,
    string Message,
    string? LogicalFileName,
    int? Line,
    int? Column);

public sealed record ReferenceValidationResult(
    int ContractVersion,
    Guid ValidationId,
    Guid ReferenceRevisionId,
    JudgeWorkerId WorkerId,
    Guid LeaseToken,
    ReferenceValidationStatus Status,
    string CompilerIdentity,
    string CompilerVersion,
    ReferenceValidationExitClassification ExitClassification,
    IReadOnlyList<ReferenceCompilerDiagnostic> Diagnostics,
    long DurationMilliseconds,
    string ExecutionBoundary,
    string? SandboxBackend,
    DateTimeOffset CompletedAtUtc,
    string? FailureCode)
{
    public const int CurrentVersion = 1;
    public const int MaximumDiagnosticCount = 100;
    public const int MaximumDiagnosticMessageLength = 2_048;
    public const int MaximumDiagnosticBytes = 32_768;

    public void Validate(IReadOnlySet<string>? allowedLogicalFiles = null)
    {
        if (ContractVersion != CurrentVersion || ValidationId == Guid.Empty || ReferenceRevisionId == Guid.Empty ||
            WorkerId.Value == Guid.Empty || LeaseToken == Guid.Empty ||
            Status is not (ReferenceValidationStatus.Succeeded or ReferenceValidationStatus.Failed or ReferenceValidationStatus.Cancelled) ||
            string.IsNullOrWhiteSpace(CompilerIdentity) || CompilerIdentity.Length > 128 ||
            string.IsNullOrWhiteSpace(CompilerVersion) || CompilerVersion.Length > 128 ||
            string.IsNullOrWhiteSpace(ExecutionBoundary) || ExecutionBoundary.Length > 64 ||
            DurationMilliseconds is < 0 or > 300_000 || Diagnostics.Count > MaximumDiagnosticCount)
            throw Invalid();

        var bytes = 0;
        foreach (var diagnostic in Diagnostics)
        {
            if (string.IsNullOrWhiteSpace(diagnostic.Message) || diagnostic.Message.Length > MaximumDiagnosticMessageLength ||
                diagnostic.Code?.Length > 64 || diagnostic.Line is < 1 || diagnostic.Column is < 1 ||
                diagnostic.LogicalFileName is { } file && (!ReferenceValidationContractRules.IsSafeLogicalFileName(file) ||
                    allowedLogicalFiles is not null && !allowedLogicalFiles.Contains(file)))
                throw Invalid();
            bytes += System.Text.Encoding.UTF8.GetByteCount(diagnostic.Message);
        }

        if (bytes > MaximumDiagnosticBytes)
            throw Invalid();
    }

    private static JudgeContractException Invalid() =>
        new(JudgeFailureCode.InvalidContract, "Reference validation result contract is invalid.");
}

public sealed record ReferenceValidationCancellation(int ContractVersion, Guid ValidationId, DateTimeOffset RequestedAtUtc);

internal static class ReferenceValidationContractRules
{
    public static bool IsSafeLogicalFileName(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && value == Path.GetFileName(value) &&
        value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && value is not "." and not "..";
}
