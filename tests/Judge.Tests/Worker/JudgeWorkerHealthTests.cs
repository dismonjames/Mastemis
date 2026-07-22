using Mastemis.Judge.Worker;
using Mastemis.Sandbox.Contracts;

namespace Mastemis.Judge.Tests.Worker;

public sealed class JudgeWorkerHealthTests
{
    [Fact]
    public void Readiness_requires_server_authentication_toolchains_workspace_and_mandatory_isolation()
    {
        var health = new JudgeWorkerHealthState();
        var sandbox = new SandboxCapabilities(true, "podman", "5", true, true, true, true, true, true, false, null);
        health.Update(state => state with
        {
            ServerConnected = true,
            Authenticated = true,
            Sandbox = sandbox,
            CppAvailable = true,
            DotnetAvailable = true,
            WorkspaceWritable = true,
            Capacity = 1
        });

        Assert.True(health.Snapshot.Ready);
        health.Update(state => state with { Sandbox = sandbox with { NetworkIsolation = false } });
        Assert.False(health.Snapshot.Ready);
    }
}
