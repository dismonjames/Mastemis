using Mastemis.Mas.Runtime.Random;

namespace Mastemis.Mas.Tests.Runtime;

public sealed class RandomTests
{
    [Fact]
    public void Splitmix64_has_versioned_golden_sequence()
    {
        var random = new SplitMix64Random(42);
        Assert.Equal(new ulong[] { 13679457532755275413, 2949826092126892291, 5139283748462763858 },
            new[] { random.NextUInt64(), random.NextUInt64(), random.NextUInt64() });
        Assert.Equal("splitmix64-v1", SplitMix64Random.AlgorithmVersion);
    }
}
