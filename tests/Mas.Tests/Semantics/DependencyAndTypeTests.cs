using Mastemis.Mas.Language.Semantics;
using Mastemis.Mas.Language.Syntax;

namespace Mastemis.Mas.Tests.Semantics;

public sealed class DependencyAndTypeTests
{
    [Theory]
    [InlineData("test 1 { a = a input = a }", "mas.semantic.self_dependency")]
    [InlineData("test 1 { a = b b = c c = a input = a }", "mas.semantic.dependency_cycle")]
    [InlineData("test 1 { a = [1, \"x\"] input = a }", "mas.semantic.array_element_type")]
    [InlineData("test 1 { a = -true input = a }", "mas.semantic.unary_type")]
    [InlineData("test 1 { a = 1 + true input = a }", "mas.semantic.binary_type")]
    [InlineData("test 1 { a = uniqueArray(3, int(1, 2)) input = a }", "mas.semantic.unique_infeasible")]
    [InlineData("test 1 { g = simpleGraph(3, 4) input = g }", "mas.semantic.graph_feasibility")]
    public void Reports_stable_dependency_and_type_diagnostics(string source, string code)
    {
        var model = new MasSemanticValidator().Validate(SyntaxTree.Parse(source));
        Assert.Contains(model.Diagnostics, x => x.Code == code);
    }

    [Fact]
    public void Rejects_duplicate_named_test_groups()
    {
        var model = new MasSemanticValidator().Validate(SyntaxTree.Parse("test 1 group x { input = 1 } test 1 group x { input = 2 }"));
        Assert.Contains(model.Diagnostics, x => x.Code == "mas.semantic.group_duplicate");
    }
}
