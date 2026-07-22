namespace Mastemis.Mas.Runtime.Random;

public sealed class SplitMix64Random(ulong seed)
{
    private ulong _state = seed;
    public const string AlgorithmVersion = "splitmix64-v1";
    public ulong NextUInt64()
    {
        var z = _state += 0x9E3779B97F4A7C15UL; z = (z ^ z >> 30) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ z >> 27) * 0x94D049BB133111EBUL; return z ^ z >> 31;
    }
    public long NextInt64(long minimum, long maximum)
    {
        if (minimum > maximum) throw new ArgumentOutOfRangeException(nameof(minimum));
        var range = unchecked((ulong)(maximum - minimum)) + 1UL; if (range == 0) return unchecked((long)NextUInt64());
        var threshold = unchecked(0UL - range) % range; ulong value; do value = NextUInt64(); while (value < threshold);
        return checked(minimum + (long)(value % range));
    }
    public double NextDouble(double minimum, double maximum)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || minimum > maximum) throw new ArgumentOutOfRangeException(nameof(minimum));
        var unit = (NextUInt64() >> 11) * (1.0 / (1UL << 53)); return minimum + (maximum - minimum) * unit;
    }
}
