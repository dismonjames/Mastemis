using System.Globalization;
using System.Text;
using Mastemis.Mas.Language.Diagnostics;
using Mastemis.Mas.Language.Syntax.Tokens;
using Mastemis.Mas.Language.Text;

namespace Mastemis.Mas.Language.Lexing;

public sealed class MasLexer(SourceText source)
{
    private readonly DiagnosticBag _diagnostics = [];
    private int _position;
    public IReadOnlyList<MasDiagnostic> Diagnostics => _diagnostics;
    public IReadOnlyList<SyntaxToken> Lex()
    {
        var tokens = new List<SyntaxToken>(); SyntaxToken token;
        do { token = Next(); if (token.Kind != SyntaxKind.BadToken) tokens.Add(token); } while (token.Kind != SyntaxKind.EndOfFileToken);
        return tokens;
    }
    private SyntaxToken Next()
    {
        SkipTrivia(); var start = _position; var current = source[_position];
        if (current == '\0') return Token(SyntaxKind.EndOfFileToken, start, 0);
        if (char.IsAsciiLetter(current) || current == '_') return Identifier();
        if (char.IsAsciiDigit(current)) return Number();
        if (current == '"') return String();
        _position++;
        return current switch
        {
            '{' => Token(SyntaxKind.OpenBraceToken, start, 1),
            '}' => Token(SyntaxKind.CloseBraceToken, start, 1),
            '[' => Token(SyntaxKind.OpenBracketToken, start, 1),
            ']' => Token(SyntaxKind.CloseBracketToken, start, 1),
            '(' => Token(SyntaxKind.OpenParenthesisToken, start, 1),
            ')' => Token(SyntaxKind.CloseParenthesisToken, start, 1),
            ',' => Token(SyntaxKind.CommaToken, start, 1),
            ':' => Token(SyntaxKind.ColonToken, start, 1),
            '+' => Token(SyntaxKind.PlusToken, start, 1),
            '-' => Token(SyntaxKind.MinusToken, start, 1),
            '*' => Token(SyntaxKind.StarToken, start, 1),
            '/' => Token(SyntaxKind.SlashToken, start, 1),
            '.' when Match('.') => Token(SyntaxKind.DotDotToken, start, 2),
            '=' when Match('=') => Token(SyntaxKind.EqualsEqualsToken, start, 2),
            '=' => Token(SyntaxKind.EqualsToken, start, 1),
            '!' when Match('=') => Token(SyntaxKind.BangEqualsToken, start, 2),
            '<' when Match('=') => Token(SyntaxKind.LessEqualsToken, start, 2),
            '<' => Token(SyntaxKind.LessToken, start, 1),
            '>' when Match('=') => Token(SyntaxKind.GreaterEqualsToken, start, 2),
            '>' => Token(SyntaxKind.GreaterToken, start, 1),
            _ => Bad(start, current)
        };
    }
    private void SkipTrivia()
    {
        while (true)
        {
            while (char.IsWhiteSpace(source[_position])) _position++;
            if (source[_position] == '/' && source[_position + 1] == '/') { while (source[_position] is not ('\n' or '\0')) _position++; continue; }
            if (source[_position] == '#') { while (source[_position] is not ('\n' or '\0')) _position++; continue; }
            return;
        }
    }
    private SyntaxToken Identifier()
    {
        var start = _position; while (char.IsAsciiLetterOrDigit(source[_position]) || source[_position] == '_') _position++;
        var text = source.Text[start.._position]; var kind = text switch
        {
            "test" => SyntaxKind.TestKeyword,
            "group" => SyntaxKind.GroupKeyword,
            "include" => SyntaxKind.IncludeKeyword,
            "input" => SyntaxKind.InputKeyword,
            "output" => SyntaxKind.OutputKeyword,
            "seed" => SyntaxKind.SeedKeyword,
            "true" => SyntaxKind.TrueKeyword,
            "false" => SyntaxKind.FalseKeyword,
            _ => SyntaxKind.IdentifierToken
        };
        return new(kind, new(start, _position - start), text, kind == SyntaxKind.TrueKeyword ? true : kind == SyntaxKind.FalseKeyword ? false : null);
    }
    private SyntaxToken Number()
    {
        var start = _position; while (char.IsAsciiDigit(source[_position])) _position++;
        var floating = source[_position] == '.' && source[_position + 1] != '.';
        if (floating) { _position++; if (!char.IsAsciiDigit(source[_position])) _diagnostics.Error("mas.number.fraction", "Fraction digits are required.", source, new(start, _position - start)); while (char.IsAsciiDigit(source[_position])) _position++; }
        var text = source.Text[start.._position];
        if (floating && double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)) return new(SyntaxKind.FloatToken, new(start, _position - start), text, number);
        if (!floating && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var integer)) return new(SyntaxKind.IntegerToken, new(start, _position - start), text, integer);
        _diagnostics.Error("mas.number.overflow", "Numeric literal is invalid or out of range.", source, new(start, _position - start)); return Token(SyntaxKind.BadToken, start, _position - start);
    }
    private SyntaxToken String()
    {
        var start = _position++; var value = new StringBuilder(); var terminated = false;
        while (source[_position] is not ('\0' or '\n'))
        {
            if (source[_position] == '"') { _position++; terminated = true; break; }
            if (source[_position] == '\\')
            {
                var escape = source[++_position]; if (escape is 'n' or 'r' or 't' or '"' or '\\')
                { value.Append(escape switch { 'n' => '\n', 'r' => '\r', 't' => '\t', _ => escape }); _position++; continue; }
                _diagnostics.Error("mas.string.escape", "String escape is invalid.", source, new(_position - 1, 2));
            }
            value.Append(source[_position++]);
        }
        if (!terminated) _diagnostics.Error("mas.string.unterminated", "String literal is unterminated.", source, new(start, _position - start));
        return new(SyntaxKind.StringToken, new(start, _position - start), source.Text[start.._position], value.ToString());
    }
    private bool Match(char expected) { if (source[_position] != expected) return false; _position++; return true; }
    private SyntaxToken Bad(int start, char value) { _diagnostics.Error("mas.character.invalid", "Character is invalid.", source, new(start, 1)); return Token(SyntaxKind.BadToken, start, 1); }
    private SyntaxToken Token(SyntaxKind kind, int start, int length) => new(kind, new(start, length), source.Text.Substring(start, length));
}
