using Mastemis.Sandbox.Containers;
using Mastemis.Sandbox.Contracts;
using Mastemis.Sandbox.Linux;

namespace Mastemis.Sandbox.Tests.Linux;

public sealed class OciSandboxTests
{
    [Fact]
    public void Command_enforces_mandatory_isolation_without_shell_or_privilege()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mastemis-sandbox-{Guid.NewGuid():N}"); Directory.CreateDirectory(root);
        try
        {
            var request = Request(root); var arguments = new OciCommandBuilder(new()).BuildRunArguments(request, "safe-name", Path.Combine(root, ".cid"));
            AssertPair(arguments, "--network", "none"); Assert.Contains("--read-only", arguments); AssertPair(arguments, "--cap-drop", "ALL");
            AssertPair(arguments, "--security-opt", "no-new-privileges"); Assert.Contains("--pids-limit", arguments);
            Assert.Contains("--memory", arguments); Assert.Contains("--userns", arguments); Assert.DoesNotContain("--privileged", arguments);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Capability_probe_fails_closed_when_runtime_is_missing()
    {
        var capabilities = await new OciSandboxCapabilityProbe(new("/definitely/missing/podman")).ProbeAsync(TestContext.Current.CancellationToken);
        Assert.False(capabilities.Available); Assert.False(capabilities.MeetsMandatoryRequirements);
    }

    [Fact]
    public async Task Runner_never_falls_back_to_host_execution()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mastemis-sandbox-{Guid.NewGuid():N}"); Directory.CreateDirectory(root);
        try
        {
            var result = await new OciSandboxRunner(new("/definitely/missing/podman")).RunAsync(Request(root), TestContext.Current.CancellationToken);
            Assert.Equal(SandboxExitKind.InfrastructureFailure, result.ExitKind); Assert.Contains("unavailable", result.InfrastructureDiagnostic!, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, true); }
    }

    private static SandboxRequest Request(string root) => new("localhost/mastemis-judge:0.1.0", "/workspace/build/program", [], root,
        null, Path.Combine(root, "stdout"), Path.Combine(root, "stderr"), new Dictionary<string, string>(),
        new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 128 * 1024 * 1024, 1024, 4096, 4));
    private static void AssertPair(IReadOnlyList<string> values, string key, string expected)
    { var index = values.IndexOf(key); Assert.True(index >= 0); Assert.Equal(expected, values[index + 1]); }
}

internal static class ListExtensions
{
    public static int IndexOf(this IReadOnlyList<string> source, string value)
    { for (var index = 0; index < source.Count; index++) if (source[index] == value) return index; return -1; }
}
