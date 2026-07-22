using Mastemis.Mas.Language.Text;

namespace Mastemis.Mas.Language.Diagnostics;

public sealed record MasDiagnostic(string Code, MasDiagnosticSeverity Severity, string Message, TextLocation Location,
    string? SuggestedCorrection = null);
public enum MasDiagnosticSeverity { Information, Warning, Error }
public sealed class DiagnosticBag : List<MasDiagnostic>
{
    public void Error(string code, string message, SourceText source, TextSpan span) =>
        Add(new(code, MasDiagnosticSeverity.Error, message, source.Location(span)));
}
