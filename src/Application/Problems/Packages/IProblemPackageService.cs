using Mastemis.Mas.Packaging.Validation;

namespace Mastemis.Application.Problems.Packages;

public sealed record ProblemPackageExport(Stream Content, string Sha256, long Length);
public sealed record ProblemPackageValidation(string PackageSha256, IReadOnlyList<PackageDiagnostic> Diagnostics);

public interface IProblemPackageService
{
    Task<ProblemPackageValidation> ValidateAsync(Stream package, CancellationToken cancellationToken);
    Task<ProblemPackageExport> ExportAsync(Guid problemId, CancellationToken cancellationToken);
}
