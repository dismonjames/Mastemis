using Mastemis.Mas.Language.Syntax.Nodes;

namespace Mastemis.Mas.Language.Semantics.Dependencies;

public static class DependencyGraphValidator
{
    public static IReadOnlyList<IReadOnlyList<string>> FindCycles(IReadOnlyDictionary<string, ExpressionSyntax> declarations,
        int maximumDepth = 256)
    {
        var graph = declarations.ToDictionary(x => x.Key, x => References(x.Value).Where(declarations.ContainsKey).ToArray(), StringComparer.Ordinal);
        var state = new Dictionary<string, int>(StringComparer.Ordinal); var path = new List<string>(); var cycles = new List<IReadOnlyList<string>>();
        foreach (var name in graph.Keys) Visit(name, 0);
        return cycles;
        void Visit(string name, int depth)
        {
            if (depth > maximumDepth) { cycles.Add([name]); return; }
            if (state.TryGetValue(name, out var value))
            {
                if (value == 1) { var start = path.IndexOf(name); cycles.Add(path.Skip(start).Append(name).ToArray()); }
                return;
            }
            state[name] = 1; path.Add(name); foreach (var next in graph[name]) Visit(next, depth + 1); path.RemoveAt(path.Count - 1); state[name] = 2;
        }
    }
    public static IEnumerable<string> References(ExpressionSyntax expression) => expression switch
    {
        NameExpressionSyntax name => [name.Identifier.Text],
        UnaryExpressionSyntax unary => References(unary.Operand),
        BinaryExpressionSyntax binary => References(binary.Left).Concat(References(binary.Right)),
        CallExpressionSyntax call => call.Arguments.SelectMany(References),
        ArrayExpressionSyntax array => array.Elements.SelectMany(References),
        _ => []
    };
}
