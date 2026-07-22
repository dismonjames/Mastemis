using System.Globalization;
using System.Text;
using Mastemis.Mas.Runtime.Values;

namespace Mastemis.Mas.Runtime.Formatting;

public static class MasInputFormatter
{
    public static string Format(IReadOnlyList<MasValue> values)
    {
        var output = new StringBuilder(); foreach (var value in values) { Append(output, value); output.Append('\n'); }
        return output.ToString();
    }
    private static void Append(StringBuilder output, MasValue value)
    {
        switch (value)
        {
            case MasInteger x: output.Append(x.Value.ToString(CultureInfo.InvariantCulture)); break;
            case MasFloat x: output.Append(x.Value.ToString("G17", CultureInfo.InvariantCulture)); break;
            case MasBoolean x: output.Append(x.Value ? '1' : '0'); break;
            case MasString x: output.Append(x.Value); break;
            case MasArray x: for (var i = 0; i < x.Values.Count; i++) { if (i > 0) output.Append(' '); Append(output, x.Values[i]); } break;
            case MasEdges x: output.Append(x.NodeCount).Append(' ').Append(x.Edges.Count); foreach (var edge in x.Edges) output.Append('\n').Append(edge.From).Append(' ').Append(edge.To); break;
            default: throw new InvalidOperationException("Unsupported MAS runtime value.");
        }
    }
}
