using System.Text;

namespace Mastemis.Mas.Language.Text;

public sealed class SourceText
{
    private readonly int[] _lines;
    private SourceText(string text) { Text = text; _lines = BuildLines(text); }
    public string Text { get; }
    public int Length => Text.Length;
    public char this[int index] => index >= Text.Length ? '\0' : Text[index];
    public static SourceText From(string text, int maximumBytes = 1024 * 1024)
    {
        if (Encoding.UTF8.GetByteCount(text) > maximumBytes) throw new ArgumentException("MAS source exceeds its size limit.", nameof(text));
        return new(text);
    }
    public TextLocation Location(TextSpan span) => new(span, Position(span.Start), Position(span.End));
    private LinePosition Position(int offset)
    {
        var index = Array.BinarySearch(_lines, offset); if (index < 0) index = ~index - 1;
        return new(index, offset - _lines[index]);
    }
    private static int[] BuildLines(string text)
    {
        var lines = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++) if (text[i] == '\n') lines.Add(i + 1);
        return lines.ToArray();
    }
}
