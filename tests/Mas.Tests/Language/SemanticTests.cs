using Mastemis.Mas.Language.Semantics;
using Mastemis.Mas.Language.Syntax;

namespace Mastemis.Mas.Tests.Language;

public sealed class SemanticTests
{
    [Fact]
    public void Accepts_supported_builtins_and_directives()
    {
        var model = new MasSemanticValidator().Validate(SyntaxTree.Parse("test 2 { n = int(1, 3) a = array(n, int(0, 9)) input = n input = a include boundaries }"));
        Assert.DoesNotContain(model.Diagnostics, x => x.Severity == Mastemis.Mas.Language.Diagnostics.MasDiagnosticSeverity.Error);
    }
    [Theory]
    [InlineData("test 1 { x = unknown(1) }")]
    [InlineData("test 1 { x = int(5, 1) }")]
    [InlineData("test 1 { x = y }")]
    [InlineData("test 0 { include imaginary }")]
    public void Reports_semantic_failures(string source) => Assert.NotEmpty(new MasSemanticValidator().Validate(SyntaxTree.Parse(source)).Diagnostics);
}
