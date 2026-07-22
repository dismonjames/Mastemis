using Mastemis.Mas.Language.Syntax.Nodes;

namespace Mastemis.Mas.Language.Semantics.Validation;

public static class BuiltinSemanticRules
{
    public static bool TryInteger(ExpressionSyntax expression, out long value)
    { if (expression is LiteralExpressionSyntax { Token.Value: long number }) { value = number; return true; } value = 0; return false; }
    public static long EstimateValues(CallExpressionSyntax call)
    {
        if (call.Name.Text is "array" or "uniqueArray" && call.Arguments.Count > 0 && TryInteger(call.Arguments[0], out var length)) return Math.Max(0, length);
        if (call.Name.Text == "permutation" && call.Arguments.Count > 0 && TryInteger(call.Arguments[^1], out var maximum)) return Math.Max(0, maximum);
        if (call.Name.Text is "tree" or "simpleGraph" && call.Arguments.Count > 0 && TryInteger(call.Arguments[0], out var nodes)) return Math.Max(0, nodes);
        return 1;
    }
}
