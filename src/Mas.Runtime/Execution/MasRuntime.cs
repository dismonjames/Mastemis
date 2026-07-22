using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Mastemis.Mas.Language.Semantics;
using Mastemis.Mas.Language.Syntax;
using Mastemis.Mas.Language.Syntax.Nodes;
using Mastemis.Mas.Language.Syntax.Tokens;
using Mastemis.Mas.Runtime.Formatting;
using Mastemis.Mas.Runtime.Generation;
using Mastemis.Mas.Runtime.Generation.Graphs;
using Mastemis.Mas.Runtime.Generation.Trees;
using Mastemis.Mas.Runtime.Limits;
using Mastemis.Mas.Runtime.Random;
using Mastemis.Mas.Runtime.Values;

namespace Mastemis.Mas.Runtime.Execution;

public sealed class MasRuntime(MasRuntimeLimits limits)
{
    public const string RuntimeVersion = "mas-runtime-1.0";
    public MasGenerationReport Generate(string source, ulong seed, CancellationToken cancellationToken)
    {
        var tree = SyntaxTree.Parse(source); var semantic = new MasSemanticValidator().Validate(tree, limits.MaximumTests);
        if (semantic.Diagnostics.Any(x => x.Severity == Mastemis.Mas.Language.Diagnostics.MasDiagnosticSeverity.Error))
            return new(seed, RuntimeVersion, SplitMix64Random.AlgorithmVersion, [], semantic.Diagnostics, 0);
        var random = new SplitMix64Random(seed); var tests = new List<GeneratedTest>(); var hashes = new HashSet<string>(StringComparer.Ordinal);
        var stopwatch = Stopwatch.StartNew(); long steps = 0; var duplicates = 0; var index = 1;
        foreach (var declaration in tree.Root.Tests)
        {
            if (declaration.Count > limits.MaximumTests || tests.Count + declaration.Count > limits.MaximumTests) Fail("mas.runtime.test_limit");
            var directives = declaration.Body.Statements.OfType<IncludeStatementSyntax>().Select(x => x.Directive.Text).ToArray();
            for (var caseIndex = 0; caseIndex < declaration.Count; caseIndex++)
            {
                Check(); var strategy = Strategy(directives, caseIndex); var variables = new Dictionary<string, MasValue>(StringComparer.Ordinal);
                var inputs = new List<MasValue>();
                foreach (var statement in declaration.Body.Statements)
                {
                    Check(); if (statement is not AssignmentStatementSyntax assignment) continue;
                    var value = Evaluate(assignment.Expression, variables, random, strategy, Check);
                    if (assignment.Name.Kind == SyntaxKind.InputKeyword) inputs.Add(ApplyStrategy(value, strategy, random));
                    else if (assignment.Name.Kind != SyntaxKind.OutputKeyword) variables.Add(assignment.Name.Text, value);
                }
                var input = MasInputFormatter.Format(inputs); var bytes = Encoding.UTF8.GetBytes(input);
                if (bytes.LongLength > limits.MaximumOutputBytes) Fail("mas.runtime.output_limit");
                var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(); if (!hashes.Add(hash)) duplicates++;
                tests.Add(new(index++, declaration.Group ?? "default", input, hash, strategy));
            }
        }
        return new(seed, RuntimeVersion, SplitMix64Random.AlgorithmVersion, tests, semantic.Diagnostics, duplicates);
        void Check()
        {
            cancellationToken.ThrowIfCancellationRequested(); if (++steps > limits.MaximumSteps) Fail("mas.runtime.step_limit");
            if (stopwatch.Elapsed > limits.Duration) Fail("mas.runtime.time_limit");
        }
        static void Fail(string code) => throw new MasRuntimeException(code, "MAS runtime limit or operation failed.");
    }

    private MasValue Evaluate(ExpressionSyntax expression, IReadOnlyDictionary<string, MasValue> variables,
        SplitMix64Random random, string strategy, Action check)
    {
        check(); return expression switch
        {
            LiteralExpressionSyntax x => x.Token.Value switch { long v => new MasInteger(v), double v => new MasFloat(v), bool v => new MasBoolean(v), string v => new MasString(v), _ => throw Invalid() },
            NameExpressionSyntax x => variables.TryGetValue(x.Identifier.Text, out var value) ? value : throw Invalid(),
            UnaryExpressionSyntax x => Unary(x, variables, random, strategy, check),
            BinaryExpressionSyntax x => Binary(x, variables, random, strategy, check),
            ArrayExpressionSyntax x => new MasArray(x.Elements.Select(y => Evaluate(y, variables, random, strategy, check)).ToArray()),
            CallExpressionSyntax x => Call(x, variables, random, strategy, check),
            _ => throw Invalid()
        };
    }
    private MasValue Call(CallExpressionSyntax call, IReadOnlyDictionary<string, MasValue> vars, SplitMix64Random random, string strategy, Action check)
    {
        long Int(int index) => ((MasInteger)Evaluate(call.Arguments[index], vars, random, strategy, check)).Value;
        MasValue Value(int index) => Evaluate(call.Arguments[index], vars, random, strategy, check);
        bool Bool(int index, bool fallback) => call.Arguments.Count > index ? ((MasBoolean)Value(index)).Value : fallback;
        return call.Name.Text switch
        {
            "int" => Integer(Int(0), Int(1), random, strategy),
            "float" => new MasFloat(random.NextDouble(Number(Value(0)), Number(Value(1)))),
            "bool" => new MasBoolean((random.NextUInt64() & 1) == 1),
            "choice" => Value((int)random.NextInt64(0, call.Arguments.Count - 1)),
            "string" => RandomString(CheckedLength(Int(0)), ((MasString)Value(1)).Value, random),
            "array" => Array(CheckedLength(Int(0)), call.Arguments[1], vars, random, strategy, check, false),
            "uniqueArray" => Array(CheckedLength(Int(0)), call.Arguments[1], vars, random, strategy, check, true),
            "permutation" => Permutation(call.Arguments.Count == 1 ? 1 : Int(0), Int(call.Arguments.Count - 1), random),
            "shuffle" => Transform(Value(0), random, "shuffle"),
            "sorted" => Transform(Value(0), random, "sorted"),
            "reversed" => Transform(Value(0), random, "reversed"),
            "tree" => TreeGenerator.Generate(new(CheckedNodes(Int(0)), Bool(1, true), call.Arguments.Count > 2 ? checked((int)Int(2)) : null,
                Bool(3, false), strategy == "adversarial-path" ? "path" : strategy == "adversarial-star" ? "star" : call.Arguments.Count > 4 ? ((MasString)Value(4)).Value : "random"), random),
            "simpleGraph" => GenerateGraph(CheckedNodes(Int(0)), CheckedEdges(Int(1)), Bool(2, false), Bool(3, false), Bool(4, true), strategy, random),
            _ => throw Invalid()
        };
    }
    private MasArray Array(int length, ExpressionSyntax generator, IReadOnlyDictionary<string, MasValue> vars, SplitMix64Random random, string strategy, Action check, bool unique)
    {
        var values = new List<MasValue>(length); var set = new HashSet<MasValue>(); var attempts = 0;
        while (values.Count < length)
        {
            check(); if (++attempts > Math.Max(100, length * 20)) throw new MasRuntimeException("mas.runtime.unique_impossible", "Unique generation is infeasible.");
            var value = Evaluate(generator, vars, random, strategy, check); if (!unique || set.Add(value)) values.Add(value);
        }
        return new(values, unique);
    }
    private static MasInteger Integer(long min, long max, SplitMix64Random random, string strategy) => new(strategy switch
    {
        "boundary-min" or "adversarial-min" => min,
        "boundary-max" or "adversarial-max" => max,
        "adversarial-near-min" => Math.Min(max, min + 1),
        "adversarial-near-max" => Math.Max(min, max - 1),
        _ => random.NextInt64(min, max)
    });
    private static MasString RandomString(int length, string alphabet, SplitMix64Random random)
    { if (alphabet.Length == 0) throw Invalid(); var value = new char[length]; for (var i = 0; i < length; i++) value[i] = alphabet[(int)random.NextInt64(0, alphabet.Length - 1)]; return new(new(value)); }
    private static MasArray Permutation(long min, long max, SplitMix64Random random)
    { if (max < min || max - min > 1_000_000) throw Invalid(); var values = Enumerable.Range(0, checked((int)(max - min + 1))).Select(x => (MasValue)new MasInteger(min + x)).ToArray(); Shuffle(values, random); return new(values); }
    private static MasValue Transform(MasValue value, SplitMix64Random random, string operation)
    {
        if (value is not MasArray array) throw Invalid(); var values = array.Values.ToArray(); if (operation == "shuffle") Shuffle(values, random);
        else System.Array.Sort(values, Compare); if (operation == "reversed") System.Array.Reverse(values); return new MasArray(values, array.IsUnique);
    }
    private static MasValue ApplyStrategy(MasValue value, string strategy, SplitMix64Random random) => strategy switch
    {
        "sorted" or "adversarial-sorted" => value is MasArray ? Transform(value, random, "sorted") : value,
        "reversed" or "adversarial-reversed" => value is MasArray ? Transform(value, random, "reversed") : value,
        "duplicates" or "adversarial-all-equal" => DuplicateHeavy(value),
        "adversarial-alternating" => Alternating(value),
        _ => value
    };
    private static MasValue DuplicateHeavy(MasValue value) => value switch
    {
        MasArray { IsUnique: false, Values.Count: > 0 } array => new MasArray(Enumerable.Repeat(array.Values[0], array.Values.Count).ToArray()),
        MasString { Value.Length: > 0 } text => new MasString(new string(text.Value[0], text.Value.Length)),
        _ => value
    };
    private static MasValue Alternating(MasValue value)
    {
        if (value is not MasArray { IsUnique: false, Values.Count: > 1 } array) return value; var sorted = array.Values.OrderBy(x => x, Comparer<MasValue>.Create(Compare)).ToArray();
        return new MasArray(Enumerable.Range(0, sorted.Length).Select(x => x % 2 == 0 ? sorted[0] : sorted[^1]).ToArray());
    }
    private static MasEdges GenerateGraph(int nodes, int edges, bool directed, bool connected, bool oneIndexed, string strategy, SplitMix64Random random)
    {
        var maximum = directed ? nodes * (nodes - 1) : nodes * (nodes - 1) / 2; if (strategy == "adversarial-sparse") { edges = Math.Max(connected ? nodes - 1 : 0, Math.Min(edges, nodes)); }
        if (strategy == "adversarial-dense") edges = Math.Min(maximum, Math.Max(edges, maximum - Math.Min(nodes, maximum)));
        return GraphGenerator.Generate(new(nodes, edges, directed, connected, oneIndexed), random);
    }
    private int CheckedLength(long value) => value is < 0 or > int.MaxValue || value > limits.MaximumCollectionLength ? throw new MasRuntimeException("mas.runtime.collection_limit", "Collection length exceeds policy.") : (int)value;
    private int CheckedNodes(long value) => value is < 1 or > int.MaxValue || value > limits.MaximumGraphNodes ? throw new MasRuntimeException("mas.runtime.graph_limit", "Graph size exceeds policy.") : (int)value;
    private int CheckedEdges(long value) => value is < 0 or > int.MaxValue || value > limits.MaximumGraphEdges ? throw new MasRuntimeException("mas.runtime.graph_limit", "Graph edge count exceeds policy.") : (int)value;
    private static double Number(MasValue value) => value switch { MasInteger x => x.Value, MasFloat x => x.Value, _ => throw Invalid() };
    private static MasValue Unary(UnaryExpressionSyntax value, IReadOnlyDictionary<string, MasValue> vars, SplitMix64Random random, string strategy, Action check)
    { var operand = new MasRuntime(new()).Evaluate(value.Operand, vars, random, strategy, check); return operand switch { MasInteger x when value.Operator.Kind == SyntaxKind.MinusToken => new MasInteger(-x.Value), MasFloat x when value.Operator.Kind == SyntaxKind.MinusToken => new MasFloat(-x.Value), _ => operand }; }
    private static MasValue Binary(BinaryExpressionSyntax value, IReadOnlyDictionary<string, MasValue> vars, SplitMix64Random random, string strategy, Action check)
    {
        var runtime = new MasRuntime(new()); var left = runtime.Evaluate(value.Left, vars, random, strategy, check); var right = runtime.Evaluate(value.Right, vars, random, strategy, check);
        if (left is MasInteger a && right is MasInteger b) return new MasInteger(value.Operator.Kind switch { SyntaxKind.PlusToken => checked(a.Value + b.Value), SyntaxKind.MinusToken => checked(a.Value - b.Value), SyntaxKind.StarToken => checked(a.Value * b.Value), SyntaxKind.SlashToken when b.Value != 0 => a.Value / b.Value, _ => throw Invalid() }); throw Invalid();
    }
    private static int Compare(MasValue left, MasValue right) => (left, right) switch { (MasInteger a, MasInteger b) => a.Value.CompareTo(b.Value), (MasFloat a, MasFloat b) => a.Value.CompareTo(b.Value), (MasString a, MasString b) => string.CompareOrdinal(a.Value, b.Value), _ => throw Invalid() };
    private static void Shuffle(MasValue[] values, SplitMix64Random random) { for (var i = values.Length - 1; i > 0; i--) { var j = (int)random.NextInt64(0, i); (values[i], values[j]) = (values[j], values[i]); } }
    private static string Strategy(IReadOnlyList<string> directives, int index)
    {
        var strategies = new List<string>(); if (directives.Contains("boundaries")) { strategies.Add("boundary-min"); strategies.Add("boundary-max"); }
        strategies.AddRange(directives.Where(x => x is "sorted" or "reversed" or "duplicates"));
        if (directives.Contains("adversarial")) strategies.AddRange(["adversarial-min", "adversarial-max", "adversarial-near-min", "adversarial-near-max",
            "adversarial-all-equal", "adversarial-alternating", "adversarial-sorted", "adversarial-reversed", "adversarial-path", "adversarial-star", "adversarial-sparse", "adversarial-dense"]);
        return index < strategies.Count ? strategies[index] : "random";
    }
    private static MasRuntimeException Invalid() => new("mas.runtime.invalid_operation", "MAS runtime operation is invalid.");
}
