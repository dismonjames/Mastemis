using Mastemis.Mas.Packaging.Archives;
using Mastemis.Mas.Packaging.Validation;

namespace Mastemis.Mas.Packaging.Importing;

public enum PackageImportMode { CreateNew, ReplaceDraft }
public sealed record PackageImportResult(ProblemPackageDocument Package, IReadOnlyList<PackageDiagnostic> Diagnostics);

public sealed class ProblemPackageImporter(PackageArchiveReader reader, ProblemPackageValidator validator)
{
    public async Task<PackageImportResult> InspectAsync(Stream package, CancellationToken cancellationToken)
    {
        var document = await reader.ReadAsync(package, cancellationToken); var diagnostics = validator.Validate(document);
        if (diagnostics.Any(x => x.Severity == PackageDiagnosticSeverity.Error)) throw new PackageValidationException(diagnostics);
        return new(document, diagnostics);
    }
}
