using Mastemis.Domain;
using Mastemis.Judge.Configuration;

namespace Mastemis.Judge.Tests.Worker;

public sealed class JudgeWorkerOptionsTests
{
    [Fact]
    public void Rejects_missing_secret_relative_workspace_and_unsafe_lease_schedule()
    {
        var valid = new JudgeWorkerOptions(new("https://mastemis.example"), JudgeWorkerId.New(), "secret", 2,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(30), Path.GetFullPath("workspaces"));
        valid.Validate(); Assert.Throws<ArgumentException>(() => (valid with { Secret = "" }).Validate());
        Assert.Throws<ArgumentException>(() => (valid with { WorkspaceRoot = "relative" }).Validate());
        Assert.Throws<ArgumentException>(() => (valid with { LeaseRenewalInterval = valid.LeaseDuration }).Validate());
    }
}
