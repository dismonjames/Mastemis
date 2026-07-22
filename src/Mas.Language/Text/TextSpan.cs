namespace Mastemis.Mas.Language.Text;

public readonly record struct TextSpan(int Start, int Length)
{
    public int End => checked(Start + Length);
    public static TextSpan FromBounds(int start, int end) => new(start, end - start);
}
public readonly record struct LinePosition(int Line, int Character);
public readonly record struct TextLocation(TextSpan Span, LinePosition Start, LinePosition End);
