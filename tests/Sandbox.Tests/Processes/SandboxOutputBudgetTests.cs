using Mastemis.Sandbox.Resources;

namespace Mastemis.Sandbox.Tests.Processes;

public sealed class SandboxOutputBudgetTests
{
    [Fact]
    public async Task Stdout_and_stderr_share_one_atomic_budget()
    {
        var budget = new SandboxOutputBudget(1000);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => budget.TryConsume(60))));

        Assert.Equal(16, results.Count(x => x));
        Assert.True(budget.ConsumedBytes > 1000);
    }
}
