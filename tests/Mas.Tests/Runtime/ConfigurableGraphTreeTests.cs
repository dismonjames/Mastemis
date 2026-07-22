using Mastemis.Mas.Runtime.Generation.Graphs;
using Mastemis.Mas.Runtime.Generation.Trees;
using Mastemis.Mas.Runtime.Random;

namespace Mastemis.Mas.Tests.Runtime;

public sealed class ConfigurableGraphTreeTests
{
    [Theory]
    [InlineData(true, "path")]
    [InlineData(false, "star")]
    [InlineData(true, "random")]
    public void Trees_have_valid_labels_connectivity_and_n_minus_one_edges(bool oneIndexed, string shape)
    {
        var root = oneIndexed ? 3 : 2; var tree = TreeGenerator.Generate(new(20, oneIndexed, root, true, shape), new(9));
        Assert.Equal(19, tree.Edges.Count); Assert.Equal(19, tree.Edges.Distinct().Count());
        var labels = tree.Edges.SelectMany(x => new[] { x.From, x.To }).ToHashSet();
        Assert.All(labels, x => Assert.InRange(x, oneIndexed ? 1 : 0, oneIndexed ? 20 : 19));
        Assert.Equal(tree.Edges, TreeGenerator.Generate(new(20, oneIndexed, root, true, shape), new(9)).Edges);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void Graphs_respect_direction_connectivity_and_indexing(bool directed, bool connected, bool oneIndexed)
    {
        var graph = GraphGenerator.Generate(new(12, 20, directed, connected, oneIndexed), new(7));
        Assert.Equal(20, graph.Edges.Count); Assert.Equal(20, graph.Edges.Distinct().Count()); Assert.DoesNotContain(graph.Edges, x => x.From == x.To);
        if (!directed) Assert.All(graph.Edges, x => Assert.True(x.From < x.To));
        Assert.Equal(graph.Edges, GraphGenerator.Generate(new(12, 20, directed, connected, oneIndexed), new(7)).Edges);
    }
}
