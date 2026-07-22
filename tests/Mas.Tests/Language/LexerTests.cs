using Mastemis.Mas.Language.Lexing;
using Mastemis.Mas.Language.Syntax.Tokens;
using Mastemis.Mas.Language.Text;

namespace Mastemis.Mas.Tests.Language;

public sealed class LexerTests
{
    [Fact]
    public void Tokenizes_keywords_numbers_strings_comments_and_operators()
    {
        var lexer = new MasLexer(SourceText.From("test 2 { seed = 4 // x\n input = int(-1, 2.5) include sorted output = \"a\\n\" }"));
        var tokens = lexer.Lex();
        Assert.Empty(lexer.Diagnostics); Assert.Contains(tokens, x => x.Kind == SyntaxKind.TestKeyword);
        Assert.Contains(tokens, x => x.Kind == SyntaxKind.FloatToken && Equals(x.Value, 2.5));
        Assert.Contains(tokens, x => x.Kind == SyntaxKind.StringToken && Equals(x.Value, "a\n"));
    }

    [Theory]
    [InlineData("\"")]
    [InlineData("1.")]
    [InlineData("@")]
    [InlineData("999999999999999999999999999")]
    public void Reports_invalid_input_without_crashing(string source)
    {
        var lexer = new MasLexer(SourceText.From(source)); _ = lexer.Lex(); Assert.NotEmpty(lexer.Diagnostics);
    }
}
