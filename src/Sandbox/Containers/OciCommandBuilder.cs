using System.Globalization;
using Mastemis.Sandbox.Contracts;
using Mastemis.Sandbox.Linux;

namespace Mastemis.Sandbox.Containers;

public sealed class OciCommandBuilder(OciSandboxOptions options)
{
    public IReadOnlyList<string> BuildRunArguments(SandboxRequest request, string containerName, string cidFile)
    {
        options.Validate();
        var limits = request.Limits; var cpuSeconds = Math.Max(1, (int)Math.Ceiling(limits.CpuTime.TotalSeconds));
        var arguments = new List<string> { "run", "--rm", "--name", containerName, "--cidfile", cidFile,
            "--network", "none", "--read-only", "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
            "--userns", "keep-id", "--user", options.ContainerUser, "--pids-limit", limits.ProcessCount.ToString(CultureInfo.InvariantCulture),
            "--memory", limits.MemoryBytes.ToString(CultureInfo.InvariantCulture), "--memory-swap", limits.MemoryBytes.ToString(CultureInfo.InvariantCulture),
            "--cpus", "1", "--ulimit", $"cpu={cpuSeconds}:{cpuSeconds}", "--ulimit", $"fsize={limits.FileBytes}:{limits.FileBytes}",
            "--workdir", "/workspace", "--mount", $"type=bind,src={Path.GetFullPath(request.WorkspacePath)},dst=/workspace,rw,rbind",
            "--tmpfs", $"/tmp:rw,noexec,nosuid,nodev,size={Math.Min(limits.FileBytes, 67_108_864)}", "--entrypoint", request.Executable };
        foreach (var variable in request.Environment.OrderBy(x => x.Key, StringComparer.Ordinal))
        { arguments.Add("--env"); arguments.Add($"{variable.Key}={variable.Value}"); }
        arguments.Add(request.Image); arguments.AddRange(request.Arguments); return arguments;
    }
}
