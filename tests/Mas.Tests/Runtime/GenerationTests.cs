using Mastemis.Mas.Runtime.Execution;
using Mastemis.Mas.Runtime.Generation;
using Mastemis.Mas.Runtime.Limits;

namespace Mastemis.Mas.Tests.Runtime;

public sealed class GenerationTests
{
    [Fact]
    public void Same_source_and_seed_produce_identical_output()
    {
        const string source = "test 4 { n = int(1, 5) a = array(n, int(-2, 2)) input = n input = a include boundaries }";
        var runtime = new MasRuntime(new()); var first = runtime.Generate(source, 99, TestContext.Current.CancellationToken);
        var second = runtime.Generate(source, 99, TestContext.Current.CancellationToken);
        Assert.Equal(first.Tests.Select(x => x.Input), second.Tests.Select(x => x.Input)); Assert.Equal(4, first.Tests.Count);
        Assert.Equal("boundary-min", first.Tests[0].Strategy); Assert.Equal("boundary-max", first.Tests[1].Strategy);
    }

    [Fact]
    public void Collections_transform_and_format_deterministically()
    {
        const string source = "test 1 { p = permutation(5) s = reversed(sorted(p)) input = s }";
        var result = new MasRuntime(new()).Generate(source, 1, TestContext.Current.CancellationToken);
        Assert.Equal("5 4 3 2 1\n", Assert.Single(result.Tests).Input);
    }

    [Fact]
    public void Tree_and_graph_satisfy_invariants()
    {
        const string source = "test 1 { t = tree(10) g = simpleGraph(8, 12) input = t input = g }";
        var input = Assert.Single(new MasRuntime(new()).Generate(source, 7, TestContext.Current.CancellationToken).Tests).Input;
        var lines = input.Split('\n', StringSplitOptions.RemoveEmptyEntries); Assert.Equal("10 9", lines[0]);
        Assert.Equal(10 + 1 + 12, lines.Length);
    }

    [Fact]
    public void Impossible_unique_generation_is_bounded()
    {
        const string source = "test 1 { a = uniqueArray(3, int(1, 2)) input = a }";
        var result = new MasRuntime(new()).Generate(source, 1, TestContext.Current.CancellationToken);
        Assert.Empty(result.Tests); Assert.Contains(result.Diagnostics, x => x.Code == "mas.semantic.unique_infeasible");
    }

    [Fact]
    public void Enforces_output_test_collection_and_cancellation_limits()
    {
        Assert.Throws<MasRuntimeException>(() => new MasRuntime(new(MaximumOutputBytes: 2)).Generate("test 1 { input = string(5, \"a\") }", 1, TestContext.Current.CancellationToken));
        Assert.Throws<MasRuntimeException>(() => new MasRuntime(new(MaximumCollectionLength: 2)).Generate("test 1 { input = array(3, int(1, 2)) }", 1, TestContext.Current.CancellationToken));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => new MasRuntime(new()).Generate("test 1 { input = int(1, 2) }", 1, cancelled.Token));
    }

    [Fact]
    public void Semantic_errors_do_not_execute()
    {
        var result = new MasRuntime(new()).Generate("test 1 { input = unknown(1) }", 1, TestContext.Current.CancellationToken);
        Assert.Empty(result.Tests); Assert.NotEmpty(result.Diagnostics);
    }
}
