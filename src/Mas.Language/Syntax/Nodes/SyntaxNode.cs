using Mastemis.Mas.Language.Text;

namespace Mastemis.Mas.Language.Syntax.Nodes;

public abstract record SyntaxNode(TextSpan Span);
public abstract record StatementSyntax(TextSpan Span) : SyntaxNode(Span);
public abstract record ExpressionSyntax(TextSpan Span) : SyntaxNode(Span);
