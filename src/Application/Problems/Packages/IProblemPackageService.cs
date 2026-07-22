using Mastemis.Mas.Packaging.Validation;

namespace Mastemis.Application.Problems.Packages;

public sealed record ProblemPackageExport(Guid ExportId, Stream Content, string Sha256, long Length,
    DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc);
public sealed record ProblemPackageValidation(string PackageSha256, IReadOnlyList<PackageDiagnostic> Diagnostics);
public sealed record ProblemPackageImport(Guid ImportId, Guid ProblemId, string PackageSha256, string Mode);
public sealed record ProblemPackageExportMetadata(Guid ExportId, Guid ProblemId, int ProblemVersion, bool IncludeHidden,
    string FormatVersion, string Sha256, long Length, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc,
    string Status, string? FailureCode);

public interface IProblemPackageService
{
    Task<ProblemPackageValidation> ValidateAsync(Stream package, CancellationToken cancellationToken);
    Task<ProblemPackageExport> ExportAsync(Guid problemId, string idempotencyKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProblemPackageExportMetadata>> ListExportsAsync(Guid problemId, CancellationToken cancellationToken);
    Task<ProblemPackageExport> OpenExportAsync(Guid problemId, Guid exportId, CancellationToken cancellationToken);
    Task ExpireExportAsync(Guid problemId, Guid exportId, CancellationToken cancellationToken);
    Task<ProblemPackageImport> CreateNewAsync(Stream package, string idempotencyKey, CancellationToken cancellationToken);
    Task<ProblemPackageImport> ReplaceDraftAsync(Guid problemId, int expectedVersion, Stream package,
        string idempotencyKey, CancellationToken cancellationToken);
}
