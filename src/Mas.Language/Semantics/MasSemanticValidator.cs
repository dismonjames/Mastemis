using Mastemis.Mas.Language.Diagnostics;
using Mastemis.Mas.Language.Semantics.Dependencies;
using Mastemis.Mas.Language.Semantics.Validation;
using Mastemis.Mas.Language.Syntax;
using Mastemis.Mas.Language.Syntax.Nodes;
using Mastemis.Mas.Language.Syntax.Tokens;

namespace Mastemis.Mas.Language.Semantics;

public sealed class MasSemanticValidator
{
    private static readonly Dictionary<string, FunctionSymbol> Functions = new(StringComparer.Ordinal)
    {
        ["int"] = new("int", 2, 2, MasType.Integer),
        ["float"] = new("float", 2, 2, MasType.Float),
        ["bool"] = new("bool", 0, 0, MasType.Boolean),
        ["choice"] = new("choice", 1, 1024, MasType.Generated),
        ["string"] = new("string", 2, 2, MasType.String),
        ["array"] = new("array", 2, 2, MasType.Array),
        ["uniqueArray"] = new("uniqueArray", 2, 2, MasType.Array),
        ["permutation"] = new("permutation", 1, 2, MasType.Permutation),
        ["shuffle"] = new("shuffle", 1, 1, MasType.Array),
        ["sorted"] = new("sorted", 1, 1, MasType.Array),
        ["reversed"] = new("reversed", 1, 1, MasType.Array),
        ["tree"] = new("tree", 1, 5, MasType.Tree),
        ["simpleGraph"] = new("simpleGraph", 2, 5, MasType.Graph)
    };
    private static readonly HashSet<string> Directives = new(StringComparer.Ordinal)
        { "boundaries", "random", "sorted", "reversed", "duplicates", "adversarial" };
    public SemanticModel Validate(SyntaxTree tree, int maximumTests = 10_000, int maximumVariables = 1_000)
    {
        var diagnostics = new DiagnosticBag(); diagnostics.AddRange(tree.Diagnostics); var variables = new Dictionary<string, VariableSymbol>(StringComparer.Ordinal);
        var totalTests = 0; var groups = new HashSet<string>(StringComparer.Ordinal); long estimatedValues = 0;
        foreach (var test in tree.Root.Tests)
        {
            variables.Clear(); var declarations = test.Body.Statements.OfType<AssignmentStatementSyntax>()
                .Where(x => x.Name.Kind == SyntaxKind.IdentifierToken).GroupBy(x => x.Name.Text, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First().Expression, StringComparer.Ordinal);
            foreach (var duplicate in test.Body.Statements.OfType<AssignmentStatementSyntax>().Where(x => x.Name.Kind == SyntaxKind.IdentifierToken)
                .GroupBy(x => x.Name.Text, StringComparer.Ordinal).Where(x => x.Count() > 1))
                Error("mas.semantic.variable_duplicate", "Variable is already declared.", duplicate.Skip(1).First().Name.Span);
            foreach (var cycle in DependencyGraphValidator.FindCycles(declarations))
                Error(cycle.Count == 2 && cycle[0] == cycle[1] ? "mas.semantic.self_dependency" : "mas.semantic.dependency_cycle",
                    "Variable dependency cycle is not allowed.", declarations[cycle[0]].Span);
            if (test.Group is { } group && !groups.Add(group)) Error("mas.semantic.group_duplicate", "Test group declaration is duplicated.", test.Span);
            totalTests = checked(totalTests + test.Count); if (test.Count is < 1 || test.Count > maximumTests)
                Error("mas.semantic.test_count", "Test count is outside policy.", test.Span);
            foreach (var statement in test.Body.Statements)
            {
                if (statement is IncludeStatementSyntax include && !Directives.Contains(include.Directive.Text))
                    Error("mas.semantic.directive", "Directive is unsupported.", include.Span);
                if (statement is SeedStatementSyntax seed && Infer(seed.Expression, null) != MasType.Integer)
                    Error("mas.semantic.seed", "Seed must be an integer.", seed.Span);
                if (statement is not AssignmentStatementSyntax assignment) continue;
                if (assignment.Name.Kind is SyntaxKind.InputKeyword or SyntaxKind.OutputKeyword) { _ = Infer(assignment.Expression, null); continue; }
                if (variables.ContainsKey(assignment.Name.Text)) continue;
                var refs = new HashSet<string>(StringComparer.Ordinal); var type = Infer(assignment.Expression, refs);
                variables[assignment.Name.Text] = new(assignment.Name.Text, type);
                estimatedValues = checked(estimatedValues + Estimate(assignment.Expression) * Math.Max(1, test.Count));
                if (variables.Count > maximumVariables) Error("mas.semantic.variable_limit", "Variable count exceeds policy.", assignment.Name.Span);
            }
        }
        if (totalTests > maximumTests) Error("mas.semantic.test_total", "Total requested tests exceed policy.", tree.Root.Span);
        if (estimatedValues > 100_000_000) Error("mas.semantic.estimated_values", "Estimated generated value count exceeds policy.", tree.Root.Span);
        return new(tree.Root, variables, diagnostics.ToArray());

        MasType Infer(ExpressionSyntax expression, HashSet<string>? refs) => expression switch
        {
            LiteralExpressionSyntax x => x.Token.Kind switch
            {
                SyntaxKind.IntegerToken => MasType.Integer,
                SyntaxKind.FloatToken => MasType.Float,
                SyntaxKind.StringToken => MasType.String,
                SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword => MasType.Boolean,
                _ => MasType.Error
            },
            NameExpressionSyntax x => Name(x, refs),
            UnaryExpressionSyntax x => Unary(x, refs),
            BinaryExpressionSyntax x => Binary(x, refs),
            ArrayExpressionSyntax x => Array(x, refs),
            CallExpressionSyntax x => Call(x, refs),
            _ => MasType.Error
        };
        MasType Name(NameExpressionSyntax value, HashSet<string>? refs)
        { refs?.Add(value.Identifier.Text); if (!variables.TryGetValue(value.Identifier.Text, out var symbol)) { Error("mas.semantic.undeclared", "Variable is used before declaration.", value.Span); return MasType.Error; } return symbol.Type; }
        MasType Unary(UnaryExpressionSyntax value, HashSet<string>? refs)
        { var operand = Infer(value.Operand, refs); if (operand is not (MasType.Integer or MasType.Float)) Error("mas.semantic.unary_type", "Unary operator requires a numeric operand.", value.Span); return operand; }
        MasType Binary(BinaryExpressionSyntax value, HashSet<string>? refs)
        { var left = Infer(value.Left, refs); var right = Infer(value.Right, refs); if (left != right || left is not (MasType.Integer or MasType.Float)) Error("mas.semantic.binary_type", "Binary operand types are incompatible.", value.Span); return left; }
        MasType Array(ArrayExpressionSyntax value, HashSet<string>? refs) { var types = value.Elements.Select(x => Infer(x, refs)).Distinct().ToArray(); if (types.Length > 1) Error("mas.semantic.array_element_type", "Array elements must have one type.", value.Span); return MasType.Array; }
        MasType Call(CallExpressionSyntax value, HashSet<string>? refs)
        {
            if (!Functions.TryGetValue(value.Name.Text, out var function)) { Error("mas.semantic.function_unknown", "Built-in function is unknown.", value.Name.Span); return MasType.Error; }
            if (value.Arguments.Count < function.MinimumArguments || value.Arguments.Count > function.MaximumArguments)
                Error("mas.semantic.argument_count", "Built-in argument count is invalid.", value.Span);
            foreach (var argument in value.Arguments) _ = Infer(argument, refs);
            if (value.Name.Text is "int" or "float" && value.Arguments.Count == 2 && TryNumber(value.Arguments[0], out var min) && TryNumber(value.Arguments[1], out var max) && min > max)
                Error("mas.semantic.range", "Minimum cannot exceed maximum.", value.Span);
            if (value.Name.Text == "uniqueArray" && value.Arguments.Count == 2 && BuiltinSemanticRules.TryInteger(value.Arguments[0], out var length) &&
                value.Arguments[1] is CallExpressionSyntax { Name.Text: "int", Arguments.Count: 2 } generator &&
                BuiltinSemanticRules.TryInteger(generator.Arguments[0], out var lower) && BuiltinSemanticRules.TryInteger(generator.Arguments[1], out var upper) && length > upper - lower + 1)
                Error("mas.semantic.unique_infeasible", "Unique array length exceeds the generator domain.", value.Span);
            if (value.Name.Text == "tree" && value.Arguments.Count > 0 && BuiltinSemanticRules.TryInteger(value.Arguments[0], out var nodes) && nodes < 1)
                Error("mas.semantic.tree_nodes", "Tree node count must be positive.", value.Span);
            if (value.Name.Text == "simpleGraph" && value.Arguments.Count >= 2 && BuiltinSemanticRules.TryInteger(value.Arguments[0], out var graphNodes) &&
                BuiltinSemanticRules.TryInteger(value.Arguments[1], out var edges) && (graphNodes < 1 || edges < 0 || edges > graphNodes * (graphNodes - 1) / 2))
                Error("mas.semantic.graph_feasibility", "Graph edge count is infeasible.", value.Span);
            return function.ResultType;
        }
        static long Estimate(ExpressionSyntax expression) => expression is CallExpressionSyntax call ? BuiltinSemanticRules.EstimateValues(call) : 1;
        static bool TryNumber(ExpressionSyntax expression, out double value)
        { if (expression is LiteralExpressionSyntax literal && literal.Token.Value is IConvertible convertible) { value = convertible.ToDouble(System.Globalization.CultureInfo.InvariantCulture); return true; } value = 0; return false; }
        void Error(string code, string message, Mastemis.Mas.Language.Text.TextSpan span) => diagnostics.Error(code, message, tree.Source, span);
    }
}
