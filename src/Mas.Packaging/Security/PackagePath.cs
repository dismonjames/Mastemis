using System.Text;
using Mastemis.Mas.Packaging.Validation;

namespace Mastemis.Mas.Packaging.Security;

public static class PackagePath
{
    public static string Normalize(string value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength || value.IndexOf('\0') >= 0 ||
            value.StartsWith('/') || value.StartsWith('\\') || value.Contains('\\') || value.Contains(':')) Invalid();
        var normalized = value.Normalize(NormalizationForm.FormC);
        var segments = normalized.Split('/');
        if (segments.Any(x => x.Length == 0 || x is "." or ".." || x.EndsWith(' ') || x.EndsWith('.'))) Invalid();
        return string.Join('/', segments);
    }
    private static void Invalid() => throw new PackageValidationException([
        new("package.path.invalid", PackageDiagnosticSeverity.Error, "Archive path is unsafe.")]);
}
