namespace Mastemis.Mas.Language.Syntax.Tokens;

public enum SyntaxKind
{
    BadToken, EndOfFileToken, IdentifierToken, IntegerToken, FloatToken, StringToken, TrueKeyword, FalseKeyword,
    TestKeyword, GroupKeyword, IncludeKeyword, InputKeyword, OutputKeyword, SeedKeyword,
    OpenBraceToken, CloseBraceToken, OpenBracketToken, CloseBracketToken, OpenParenthesisToken, CloseParenthesisToken,
    CommaToken, ColonToken, EqualsToken, DotDotToken, PlusToken, MinusToken, StarToken, SlashToken,
    EqualsEqualsToken, BangEqualsToken, LessToken, LessEqualsToken, GreaterToken, GreaterEqualsToken
}
