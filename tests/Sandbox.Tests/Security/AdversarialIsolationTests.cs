using Mastemis.Sandbox.Contracts;
using Mastemis.Sandbox.Linux;

namespace Mastemis.Sandbox.Tests.Security;

public sealed class AdversarialIsolationTests
{
    [Theory]
    [InlineData("infinite-loop", "while :; do :; done")]
    [InlineData("allocation-loop", "x=x; while :; do x=$x$x$x$x; done")]
    [InlineData("process-bomb", ":(){ :|:& };:")]
    [InlineData("endless-stdout", "while :; do echo 1234567890; done")]
    [InlineData("endless-stderr", "while :; do echo 1234567890 >&2; done")]
    [InlineData("large-file", "dd if=/dev/zero of=/workspace/run/large bs=1048576 count=64")]
    [InlineData("network-attempt", "exec 3<>/dev/tcp/1.1.1.1/80")]
    [InlineData("background-child", "sleep 30 & exit 0")]
    [InlineData("host-environment", "test -z \"$MASTEMIS_TEST_SECRET\"")]
    public async Task Adversarial_command_remains_inside_mandatory_oci_profile(string scenario, string command)
    {
        var options = new OciSandboxOptions(); var capabilities = await new OciSandboxCapabilityProbe(options).ProbeAsync(TestContext.Current.CancellationToken);
        if (!capabilities.MeetsMandatoryRequirements) Assert.Skip($"Podman adversarial test '{scenario}' skipped: {capabilities.UnavailableReason}");
        var root = Path.Combine(Path.GetTempPath(), $"mastemis-adversarial-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root); Directory.CreateDirectory(Path.Combine(root, "run"));
        try
        {
            var request = new SandboxRequest(options.Image, "/bin/bash", ["-c", command], root, null,
                Path.Combine(root, "stdout"), Path.Combine(root, "stderr"), new Dictionary<string, string>(),
                new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 64 * 1024 * 1024, 4096, 8192, 4));
            var result = await new OciSandboxRunner(options).RunAsync(request, TestContext.Current.CancellationToken);
            Assert.NotEqual(SandboxExitKind.InfrastructureFailure, result.ExitKind);
            if (scenario == "infinite-loop") Assert.Equal(SandboxExitKind.TimedOut, result.ExitKind);
            if (scenario is "endless-stdout" or "endless-stderr") Assert.Equal(SandboxResourceViolation.Output, result.ResourceViolation);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
