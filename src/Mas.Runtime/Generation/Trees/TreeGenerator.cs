using Mastemis.Mas.Runtime.Generation;
using Mastemis.Mas.Runtime.Random;
using Mastemis.Mas.Runtime.Values;

namespace Mastemis.Mas.Runtime.Generation.Trees;

public sealed record TreeOptions(int Nodes, bool OneIndexed = true, int? Root = null, bool ShuffleLabels = false, string Shape = "random");
public static class TreeGenerator
{
    public static MasEdges Generate(TreeOptions options, SplitMix64Random random)
    {
        if (options.Nodes < 1) throw Invalid(); var offset = options.OneIndexed ? 1 : 0;
        var labels = Enumerable.Range(offset, options.Nodes).ToArray(); var root = options.Root ?? offset;
        if (!labels.Contains(root) || options.Shape is not ("random" or "path" or "star")) throw Invalid();
        var remaining = labels.Where(x => x != root).ToArray(); if (options.ShuffleLabels) Shuffle(remaining, random);
        var order = new[] { root }.Concat(remaining).ToArray(); var edges = new List<(int, int)>(Math.Max(0, options.Nodes - 1));
        for (var index = 1; index < order.Length; index++)
        {
            var parent = options.Shape switch { "path" => order[index - 1], "star" => root, _ => order[(int)random.NextInt64(0, index - 1)] };
            edges.Add((parent, order[index]));
        }
        return new(options.Nodes, edges);
    }
    private static void Shuffle(int[] values, SplitMix64Random random)
    { for (var i = values.Length - 1; i > 0; i--) { var j = (int)random.NextInt64(0, i); (values[i], values[j]) = (values[j], values[i]); } }
    private static MasRuntimeException Invalid() => new("mas.runtime.tree_options", "Tree options are invalid.");
}
