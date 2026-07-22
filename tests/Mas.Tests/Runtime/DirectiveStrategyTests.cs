using Mastemis.Mas.Runtime.Execution;
using Mastemis.Mas.Runtime.Limits;

namespace Mastemis.Mas.Tests.Runtime;

public sealed class DirectiveStrategyTests
{
    [Fact]
    public void Duplicates_produces_duplicate_heavy_array_but_preserves_unique_arrays()
    {
        const string source = "test 1 { a = array(5, int(1, 9)) u = uniqueArray(3, int(1, 9)) input = a input = u include duplicates }";
        var values = Assert.Single(new MasRuntime(new()).Generate(source, 5, TestContext.Current.CancellationToken).Tests).Input.Split('\n');
        Assert.Single(values[0].Split(' ').Distinct()); Assert.Equal(3, values[1].Split(' ').Distinct().Count());
    }

    [Fact]
    public void Adversarial_allocates_concrete_deterministic_strategies_without_extra_tests()
    {
        const string source = "test 12 { a = array(6, int(-5, 5)) input = a include adversarial }";
        var result = new MasRuntime(new()).Generate(source, 11, TestContext.Current.CancellationToken);
        Assert.Equal(12, result.Tests.Count); Assert.Equal("adversarial-min", result.Tests[0].Strategy);
        Assert.Equal("adversarial-dense", result.Tests[^1].Strategy);
        Assert.NotEqual(result.Tests[4].Input, result.Tests[5].Input);
        Assert.Equal(result.Tests.Select(x => x.Input), new MasRuntime(new()).Generate(source, 11, TestContext.Current.CancellationToken).Tests.Select(x => x.Input));
    }

    [Fact]
    public void Adversarial_tree_shapes_are_real()
    {
        const string source = "test 10 { t = tree(6) input = t include adversarial }";
        var tests = new MasRuntime(new(MaximumTests: 20)).Generate(source, 1, TestContext.Current.CancellationToken).Tests;
        Assert.Equal("adversarial-path", tests[8].Strategy); Assert.Equal("adversarial-star", tests[9].Strategy);
        var starEdges = tests[9].Input.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(x => x.Split(' ')[0]).ToArray();
        Assert.Single(starEdges.Distinct());
    }
}
