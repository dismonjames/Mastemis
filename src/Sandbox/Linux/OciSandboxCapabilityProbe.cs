using System.Diagnostics;
using Mastemis.Sandbox.Abstractions;
using Mastemis.Sandbox.Contracts;

namespace Mastemis.Sandbox.Linux;

public sealed class OciSandboxCapabilityProbe(OciSandboxOptions options) : ISandboxCapabilityProbe
{
    public async ValueTask<SandboxCapabilities> ProbeAsync(CancellationToken cancellationToken)
    {
        options.Validate();
        if (!File.Exists(options.RuntimePath)) return Unavailable("Configured Podman executable is unavailable.");
        var version = await CommandAsync(["version", "--format", "{{.Client.Version}}"], cancellationToken);
        if (version.ExitCode != 0) return Unavailable("Podman did not report a usable client version.");
        var image = await CommandAsync(["image", "exists", options.Image], cancellationToken);
        if (image.ExitCode != 0) return Unavailable("The pinned judge image is not present locally; automatic pulls are disabled.", version.Output);
        var rootless = await CommandAsync(["info", "--format", "{{.Host.Security.Rootless}}"], cancellationToken);
        if (rootless.ExitCode != 0 || !bool.TryParse(rootless.Output.Trim(), out var isRootless))
            return Unavailable("Podman isolation capabilities could not be determined.", version.Output);
        var smoke = await CommandAsync(["run", "--rm", "--network", "none", "--read-only", "--cap-drop", "ALL",
            "--security-opt", "no-new-privileges", "--pids-limit", "4", "--memory", "33554432", "--entrypoint",
            "/usr/bin/true", options.Image], cancellationToken);
        if (smoke.ExitCode != 0) return Unavailable("Podman could not enforce the mandatory isolation profile.", version.Output);
        return new(true, "podman", version.Output.Trim(), isRootless, true, true, true, true, true, false, null);
    }

    private async Task<(int ExitCode, string Output)> CommandAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(options.RuntimePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.Environment.Clear(); foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!; var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken); return (process.ExitCode, output);
    }
    private static SandboxCapabilities Unavailable(string reason, string? version = null) => new(false, "podman", version,
        false, false, false, false, false, false, false, reason);
}
