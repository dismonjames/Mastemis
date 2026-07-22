using Mastemis.Mas.Language.Syntax;
using Mastemis.Mas.Language.Syntax.Nodes;

namespace Mastemis.Mas.Tests.Language;

public sealed class ParserTests
{
    [Fact]
    public void Parses_documented_generation_example()
    {
        var tree = SyntaxTree.Parse("""
            test 20 group main {
                n = int(1, 100000)
                a = array(n, int(-1000, 1000))
                input = n
                input = a
                include boundaries
                include sorted
                include reversed
            }
            """);
        Assert.Empty(tree.Diagnostics); var test = Assert.Single(tree.Root.Tests); Assert.Equal(20, test.Count);
        Assert.Equal("main", test.Group); Assert.Equal(7, test.Body.Statements.Count);
        Assert.IsType<CallExpressionSyntax>(((AssignmentStatementSyntax)test.Body.Statements[1]).Expression);
    }

    [Theory]
    [InlineData("test 1 { n = int(1, )")]
    [InlineData("wat 1 { }")]
    [InlineData("test 1 { = 2 input = }")]
    public void Recovers_and_reports_malformed_source(string source)
    {
        var tree = SyntaxTree.Parse(source); Assert.NotEmpty(tree.Diagnostics);
    }

    [Fact]
    public void Rejects_deep_nesting_without_stack_overflow()
    {
        var source = "test 1 { x = " + new string('(', 200) + "1" + new string(')', 200) + " }";
        Assert.Contains(SyntaxTree.Parse(source).Diagnostics, x => x.Code == "mas.parser.depth");
    }
}
