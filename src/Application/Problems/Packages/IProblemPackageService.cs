using Mastemis.Mas.Packaging.Validation;

namespace Mastemis.Application.Problems.Packages;

public sealed record ProblemPackageExport(Stream Content, string Sha256, long Length);
public sealed record ProblemPackageValidation(string PackageSha256, IReadOnlyList<PackageDiagnostic> Diagnostics);
public sealed record ProblemPackageImport(Guid ImportId, Guid ProblemId, string PackageSha256, string Mode);

public interface IProblemPackageService
{
    Task<ProblemPackageValidation> ValidateAsync(Stream package, CancellationToken cancellationToken);
    Task<ProblemPackageExport> ExportAsync(Guid problemId, CancellationToken cancellationToken);
    Task<ProblemPackageImport> CreateNewAsync(Stream package, string idempotencyKey, CancellationToken cancellationToken);
    Task<ProblemPackageImport> ReplaceDraftAsync(Guid problemId, int expectedVersion, Stream package,
        string idempotencyKey, CancellationToken cancellationToken);
}
