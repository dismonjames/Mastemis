using Mastemis.Mas.Language.Diagnostics;
using Mastemis.Mas.Language.Lexing;
using Mastemis.Mas.Language.Syntax.Nodes;
using Mastemis.Mas.Language.Syntax.Tokens;
using Mastemis.Mas.Language.Text;

namespace Mastemis.Mas.Language.Parsing;

public sealed class MasParser
{
    private readonly SourceText _source; private readonly IReadOnlyList<SyntaxToken> _tokens; private readonly DiagnosticBag _diagnostics = [];
    private int _position; private int _depth;
    public MasParser(SourceText source)
    {
        _source = source; var lexer = new MasLexer(source); _tokens = lexer.Lex(); _diagnostics.AddRange(lexer.Diagnostics);
    }
    public IReadOnlyList<MasDiagnostic> Diagnostics => _diagnostics;
    public CompilationUnitSyntax ParseCompilationUnit()
    {
        var tests = new List<TestDeclarationSyntax>();
        while (Current.Kind != SyntaxKind.EndOfFileToken)
        {
            var start = _position;
            if (Current.Kind == SyntaxKind.TestKeyword) tests.Add(ParseTest()); else { Error("mas.parser.test", "Expected test declaration.", Current.Span); Next(); }
            if (_position == start) Next();
        }
        return new(tests, Current, new(0, _source.Length));
    }
    private TestDeclarationSyntax ParseTest()
    {
        var start = Match(SyntaxKind.TestKeyword).Span.Start; var countToken = Match(SyntaxKind.IntegerToken);
        var count = countToken.Value is long value && value <= int.MaxValue ? (int)value : 0; string? group = null;
        if (Current.Kind == SyntaxKind.GroupKeyword) { Next(); group = Match(SyntaxKind.IdentifierToken).Text; }
        var body = ParseBlock(); return new(count, group, body, TextSpan.FromBounds(start, body.Span.End));
    }
    private BlockSyntax ParseBlock()
    {
        if (++_depth > 128) { Error("mas.parser.depth", "Syntax nesting exceeds the limit.", Current.Span); _depth--; return new([], Current.Span); }
        var open = Match(SyntaxKind.OpenBraceToken); var statements = new List<StatementSyntax>();
        while (Current.Kind is not (SyntaxKind.CloseBraceToken or SyntaxKind.EndOfFileToken))
        {
            var start = _position; statements.Add(ParseStatement()); if (_position == start) Next();
        }
        var close = Match(SyntaxKind.CloseBraceToken); _depth--;
        return new(statements, TextSpan.FromBounds(open.Span.Start, close.Span.End));
    }
    private StatementSyntax ParseStatement()
    {
        if (Current.Kind == SyntaxKind.IncludeKeyword) { Next(); return new IncludeStatementSyntax(Match(SyntaxKind.IdentifierToken)); }
        if (Current.Kind == SyntaxKind.SeedKeyword) { Next(); Match(SyntaxKind.EqualsToken); return new SeedStatementSyntax(ParseExpression()); }
        if (Current.Kind is SyntaxKind.IdentifierToken or SyntaxKind.InputKeyword or SyntaxKind.OutputKeyword)
        { var name = Next(); Match(SyntaxKind.EqualsToken); return new AssignmentStatementSyntax(name, ParseExpression()); }
        Error("mas.parser.statement", "Expected assignment or directive.", Current.Span); var bad = Next(); return new AssignmentStatementSyntax(bad, new LiteralExpressionSyntax(bad));
    }
    private ExpressionSyntax ParseExpression(int parentPrecedence = 0)
    {
        if (++_depth > 128) { Error("mas.parser.depth", "Expression nesting exceeds the limit.", Current.Span); _depth--; return new LiteralExpressionSyntax(Next()); }
        ExpressionSyntax left; var unary = UnaryPrecedence(Current.Kind);
        if (unary > 0) { var op = Next(); left = new UnaryExpressionSyntax(op, ParseExpression(unary)); }
        else left = ParsePrimary();
        while (true)
        {
            var precedence = BinaryPrecedence(Current.Kind); if (precedence == 0 || precedence <= parentPrecedence) break;
            var op = Next(); left = new BinaryExpressionSyntax(left, op, ParseExpression(precedence));
        }
        _depth--; return left;
    }
    private ExpressionSyntax ParsePrimary()
    {
        if (Current.Kind == SyntaxKind.OpenParenthesisToken) { Next(); var value = ParseExpression(); Match(SyntaxKind.CloseParenthesisToken); return value; }
        if (Current.Kind == SyntaxKind.OpenBracketToken) return ParseArray();
        if (Current.Kind == SyntaxKind.IdentifierToken)
        {
            var name = Next(); if (Current.Kind != SyntaxKind.OpenParenthesisToken) return new NameExpressionSyntax(name);
            Next(); var args = new List<ExpressionSyntax>();
            while (Current.Kind is not (SyntaxKind.CloseParenthesisToken or SyntaxKind.EndOfFileToken))
            { args.Add(ParseExpression()); if (Current.Kind != SyntaxKind.CommaToken) break; Next(); }
            var close = Match(SyntaxKind.CloseParenthesisToken); return new CallExpressionSyntax(name, args, TextSpan.FromBounds(name.Span.Start, close.Span.End));
        }
        if (Current.Kind is SyntaxKind.IntegerToken or SyntaxKind.FloatToken or SyntaxKind.StringToken or SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword)
            return new LiteralExpressionSyntax(Next());
        Error("mas.parser.expression", "Expected expression.", Current.Span); return new LiteralExpressionSyntax(Next());
    }
    private ArrayExpressionSyntax ParseArray()
    {
        var open = Next(); var elements = new List<ExpressionSyntax>();
        while (Current.Kind is not (SyntaxKind.CloseBracketToken or SyntaxKind.EndOfFileToken))
        { elements.Add(ParseExpression()); if (Current.Kind != SyntaxKind.CommaToken) break; Next(); }
        var close = Match(SyntaxKind.CloseBracketToken); return new(elements, TextSpan.FromBounds(open.Span.Start, close.Span.End));
    }
    private SyntaxToken Match(SyntaxKind kind)
    {
        if (Current.Kind == kind) return Next(); Error("mas.parser.missing", $"Expected {kind}.", Current.Span);
        return new(kind, new(Current.Span.Start, 0), string.Empty, null, true);
    }
    private SyntaxToken Current => Peek(0); private SyntaxToken Peek(int offset) => _tokens[Math.Min(_position + offset, _tokens.Count - 1)];
    private SyntaxToken Next() { var current = Current; if (_position < _tokens.Count - 1) _position++; return current; }
    private void Error(string code, string message, TextSpan span) => _diagnostics.Error(code, message, _source, span);
    private static int UnaryPrecedence(SyntaxKind kind) => kind is SyntaxKind.PlusToken or SyntaxKind.MinusToken ? 6 : 0;
    private static int BinaryPrecedence(SyntaxKind kind) => kind switch
    {
        SyntaxKind.StarToken or SyntaxKind.SlashToken => 5,
        SyntaxKind.PlusToken or SyntaxKind.MinusToken => 4,
        SyntaxKind.DotDotToken => 3,
        SyntaxKind.LessToken or SyntaxKind.LessEqualsToken or SyntaxKind.GreaterToken or SyntaxKind.GreaterEqualsToken => 2,
        SyntaxKind.EqualsEqualsToken or SyntaxKind.BangEqualsToken => 1,
        _ => 0
    };
}
