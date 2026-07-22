using Mastemis.Mas.Language.Diagnostics;
using Mastemis.Mas.Language.Parsing;
using Mastemis.Mas.Language.Syntax.Nodes;
using Mastemis.Mas.Language.Text;

namespace Mastemis.Mas.Language.Syntax;

public sealed record SyntaxTree(SourceText Source, CompilationUnitSyntax Root, IReadOnlyList<MasDiagnostic> Diagnostics)
{
    public static SyntaxTree Parse(string text)
    {
        var source = SourceText.From(text); var parser = new MasParser(source); var root = parser.ParseCompilationUnit();
        return new(source, root, parser.Diagnostics.ToArray());
    }
}
