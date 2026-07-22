namespace Mastemis.Mas.Runtime.Values;

public abstract record MasValue;
public sealed record MasInteger(long Value) : MasValue;
public sealed record MasFloat(double Value) : MasValue;
public sealed record MasBoolean(bool Value) : MasValue;
public sealed record MasString(string Value) : MasValue;
public sealed record MasArray(IReadOnlyList<MasValue> Values) : MasValue;
public sealed record MasEdges(int NodeCount, IReadOnlyList<(int From, int To)> Edges) : MasValue;
