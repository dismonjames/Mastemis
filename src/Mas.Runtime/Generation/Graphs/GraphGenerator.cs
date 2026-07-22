using Mastemis.Mas.Runtime.Generation;
using Mastemis.Mas.Runtime.Random;
using Mastemis.Mas.Runtime.Values;

namespace Mastemis.Mas.Runtime.Generation.Graphs;

public sealed record GraphOptions(int Nodes, int Edges, bool Directed = false, bool Connected = false, bool OneIndexed = true);
public static class GraphGenerator
{
    public static MasEdges Generate(GraphOptions options, SplitMix64Random random)
    {
        if (options.Nodes < 1 || options.Edges < 0 || options.Connected && options.Edges < options.Nodes - 1) throw Invalid();
        var maximum = options.Directed ? (long)options.Nodes * (options.Nodes - 1) : (long)options.Nodes * (options.Nodes - 1) / 2;
        if (options.Edges > maximum) throw Invalid(); var offset = options.OneIndexed ? 1 : 0; var result = new HashSet<(int, int)>();
        if (options.Connected) for (var i = 1; i < options.Nodes; i++) Add(offset + i, offset + (int)random.NextInt64(0, i - 1));
        var candidates = new List<(int, int)>();
        for (var a = 0; a < options.Nodes; a++) for (var b = 0; b < options.Nodes; b++)
            if (a != b && (options.Directed || a < b)) candidates.Add((a + offset, b + offset));
        for (var i = candidates.Count - 1; i > 0; i--) { var j = (int)random.NextInt64(0, i); (candidates[i], candidates[j]) = (candidates[j], candidates[i]); }
        foreach (var edge in candidates) { if (result.Count >= options.Edges) break; Add(edge.Item1, edge.Item2); }
        return new(options.Nodes, result.Order().ToArray());
        void Add(int from, int to) { if (!options.Directed && from > to) (from, to) = (to, from); result.Add((from, to)); }
    }
    private static MasRuntimeException Invalid() => new("mas.runtime.graph_options", "Graph options are invalid.");
}
