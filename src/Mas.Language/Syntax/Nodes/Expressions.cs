using Mastemis.Mas.Language.Syntax.Tokens;
using Mastemis.Mas.Language.Text;

namespace Mastemis.Mas.Language.Syntax.Nodes;

public sealed record LiteralExpressionSyntax(SyntaxToken Token) : ExpressionSyntax(Token.Span);
public sealed record NameExpressionSyntax(SyntaxToken Identifier) : ExpressionSyntax(Identifier.Span);
public sealed record UnaryExpressionSyntax(SyntaxToken Operator, ExpressionSyntax Operand)
    : ExpressionSyntax(TextSpan.FromBounds(Operator.Span.Start, Operand.Span.End));
public sealed record BinaryExpressionSyntax(ExpressionSyntax Left, SyntaxToken Operator, ExpressionSyntax Right)
    : ExpressionSyntax(TextSpan.FromBounds(Left.Span.Start, Right.Span.End));
public sealed record CallExpressionSyntax(SyntaxToken Name, IReadOnlyList<ExpressionSyntax> Arguments, TextSpan CallSpan)
    : ExpressionSyntax(CallSpan);
public sealed record ArrayExpressionSyntax(IReadOnlyList<ExpressionSyntax> Elements, TextSpan ArraySpan) : ExpressionSyntax(ArraySpan);
