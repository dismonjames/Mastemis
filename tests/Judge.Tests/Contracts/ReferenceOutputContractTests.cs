using Mastemis.Contracts.Judge;
using Mastemis.Contracts.Problems.ReferenceOutputs;
using Mastemis.Domain;

namespace Mastemis.Judge.Tests.Contracts;

public sealed class ReferenceOutputContractTests
{
    [Fact]
    public void Validates_version_language_identity_and_test_membership()
    {
        var payload = Valid(); payload.Validate();
        Assert.Throws<JudgeContractException>(() => (payload with { ContractVersion = 2 }).Validate());
        Assert.Throws<JudgeContractException>(() => (payload with { Language = "python" }).Validate());
        Assert.Throws<JudgeContractException>(() => (payload with { Tests = [payload.Tests[0], payload.Tests[0]] }).Validate());
    }

    private static ReferenceOutputJobPayload Valid() => new(1, Guid.NewGuid(), Guid.NewGuid(), ProblemId.New(), 1,
        Guid.NewGuid(), "cpp", [new("main.cpp", "problem/reference-source/00000000000000000000000000000000", new string('a', 64), 10)],
        [new(1, "problem/test-input/00000000000000000000000000000000", new string('b', 64), 2)],
        new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 64 * 1024 * 1024, 1024, 1024 * 1024, 4, 1,
            TimeSpan.FromSeconds(10), 1024 * 1024), TimeSpan.FromMinutes(1));
}
