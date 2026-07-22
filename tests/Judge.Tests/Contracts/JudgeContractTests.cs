using Mastemis.Contracts.Judge;
using Mastemis.Domain;

namespace Mastemis.Judge.Tests.Contracts;

public sealed class JudgeContractTests
{
    [Fact]
    public void Rejects_invalid_limits_and_duplicate_test_indexes()
    {
        var limits = ValidLimits();
        var request = new JudgeExecutionRequest(JudgeJobId.New(), SubmissionId.New(), JudgeWorkerId.New(), "cpp",
            [new("main.cpp", "int main(){}"u8.ToArray())],
            [new(1, Array.Empty<byte>(), Array.Empty<byte>(), "exact"), new(1, Array.Empty<byte>(), Array.Empty<byte>(), "exact")],
            limits, new("x64", new Dictionary<string, string>()));
        Assert.Equal(JudgeFailureCode.InvalidContract, Assert.Throws<JudgeContractException>(request.Validate).Code);
        Assert.Throws<JudgeContractException>(() => (limits with { MemoryBytes = 1 }).Validate());
    }

    internal static ResourceLimits ValidLimits() => new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2),
        128 * 1024 * 1024, 1024 * 1024, 8 * 1024 * 1024, 8, 100, TimeSpan.FromSeconds(10), 1024 * 1024);
}
