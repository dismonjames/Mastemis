using Mastemis.Sandbox.Abstractions;
using Mastemis.Sandbox.Containers;
using Mastemis.Sandbox.Contracts;
using Mastemis.Sandbox.Processes;
using Mastemis.Sandbox.Security;

namespace Mastemis.Sandbox.Linux;

public sealed class OciSandboxRunner(OciSandboxOptions options) : ISandboxRunner
{
    public async ValueTask<SandboxResult> RunAsync(SandboxRequest request, CancellationToken cancellationToken)
    {
        SandboxRequestValidator.Validate(request, new OciImagePolicy(options.Image));
        var probe = await new OciSandboxCapabilityProbe(options).ProbeAsync(cancellationToken);
        if (!probe.MeetsMandatoryRequirements) return new(SandboxExitKind.InfrastructureFailure, null, null, TimeSpan.Zero,
            TimeSpan.Zero, null, 0, 0, null, probe.UnavailableReason ?? "Mandatory OCI isolation capabilities are unavailable.", probe.Backend);
        var name = $"mastemis-{Guid.NewGuid():N}"; var cidFile = Path.Combine(request.WorkspacePath, $".{Guid.NewGuid():N}.cid");
        try
        {
            var arguments = new OciCommandBuilder(options).BuildRunArguments(request, name, cidFile);
            return await OciProcessRunner.RunAsync(options.RuntimePath, arguments, request, "podman", cancellationToken);
        }
        finally { await RemoveContainerAsync(name); if (File.Exists(cidFile)) File.Delete(cidFile); }
    }

    private async Task RemoveContainerAsync(string name)
    {
        var start = new System.Diagnostics.ProcessStartInfo(options.RuntimePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("rm"); start.ArgumentList.Add("--force"); start.ArgumentList.Add("--ignore"); start.ArgumentList.Add(name);
        try { using var process = System.Diagnostics.Process.Start(start); if (process is not null) await process.WaitForExitAsync(); }
        catch (System.ComponentModel.Win32Exception) { }
    }
}
