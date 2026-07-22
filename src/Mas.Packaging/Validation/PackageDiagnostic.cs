namespace Mastemis.Mas.Packaging.Validation;

public sealed record PackageDiagnostic(string Code, PackageDiagnosticSeverity Severity, string Message,
    string? Path = null, string? Property = null, string? SuggestedCorrection = null);
public enum PackageDiagnosticSeverity { Information, Warning, Error }

public sealed class PackageValidationException(IReadOnlyList<PackageDiagnostic> diagnostics)
    : Exception("The problem package is invalid.")
{
    public IReadOnlyList<PackageDiagnostic> Diagnostics { get; } = diagnostics;
}
