using Mastemis.Mas.Language.Syntax.Tokens;
using Mastemis.Mas.Language.Text;

namespace Mastemis.Mas.Language.Syntax.Nodes;

public sealed record AssignmentStatementSyntax(SyntaxToken Name, ExpressionSyntax Expression)
    : StatementSyntax(TextSpan.FromBounds(Name.Span.Start, Expression.Span.End));
public sealed record IncludeStatementSyntax(SyntaxToken Directive)
    : StatementSyntax(Directive.Span);
public sealed record SeedStatementSyntax(ExpressionSyntax Expression)
    : StatementSyntax(Expression.Span);
public sealed record BlockSyntax(IReadOnlyList<StatementSyntax> Statements, TextSpan BlockSpan) : SyntaxNode(BlockSpan);
public sealed record TestDeclarationSyntax(int Count, string? Group, BlockSyntax Body, TextSpan DeclarationSpan)
    : SyntaxNode(DeclarationSpan);
public sealed record CompilationUnitSyntax(IReadOnlyList<TestDeclarationSyntax> Tests, SyntaxToken EndOfFile, TextSpan UnitSpan)
    : SyntaxNode(UnitSpan);
