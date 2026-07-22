using Mastemis.Mas.Language.Text;

namespace Mastemis.Mas.Language.Syntax.Tokens;

public sealed record SyntaxToken(SyntaxKind Kind, TextSpan Span, string Text, object? Value = null, bool IsMissing = false);
