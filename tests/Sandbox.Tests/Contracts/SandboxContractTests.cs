using Mastemis.Sandbox.Contracts;

namespace Mastemis.Sandbox.Tests.Contracts;

public sealed class SandboxContractTests
{
    [Fact]
    public void Mandatory_capabilities_fail_closed_when_memory_control_is_missing()
    {
        var capabilities = new SandboxCapabilities(true, "test", "1", true, true, false, true, true, true, true, null);
        Assert.False(capabilities.MeetsMandatoryRequirements);
    }
}
