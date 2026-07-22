using Mastemis.Sandbox.Contracts;

namespace Mastemis.Sandbox.Security;

public static class SandboxRequestValidator
{
    public static void Validate(SandboxRequest request, OciImagePolicy imagePolicy)
    {
        imagePolicy.EnsureAllowed(request.Image);
        if (!request.NetworkDisabled || !request.Executable.StartsWith("/", StringComparison.Ordinal) ||
            request.Executable.Contains("..", StringComparison.Ordinal) || request.Arguments.Count > 128)
            throw new ArgumentException("Sandbox command is unsafe.");
        var root = Path.GetFullPath(request.WorkspacePath);
        if (!Directory.Exists(root) || IsSymlink(root)) throw new ArgumentException("Sandbox workspace is unsafe.");
        foreach (var path in new[] { request.StandardInputPath, request.StandardOutputPath, request.StandardErrorPath })
            if (path is not null) EnsureInside(root, path);
        if (request.Environment.Count > 32 || request.Environment.Any(item => !ValidEnvironmentName(item.Key) || item.Value.Length > 1000))
            throw new ArgumentException("Sandbox environment is invalid.");
        var limits = request.Limits;
        if (limits.CpuTime <= TimeSpan.Zero || limits.WallTime < limits.CpuTime || limits.MemoryBytes < 16_777_216 ||
            limits.OutputBytes < 1 || limits.FileBytes < limits.OutputBytes || limits.ProcessCount is < 1 or > 128)
            throw new ArgumentException("Sandbox limits are invalid.");
    }
    private static void EnsureInside(string root, string path)
    {
        var canonical = Path.GetFullPath(path);
        if (!canonical.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || IsSymlink(canonical))
            throw new ArgumentException("Sandbox I/O path escaped the workspace.");
    }
    private static bool IsSymlink(string path) => File.Exists(path) || Directory.Exists(path)
        ? (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 : false;
    private static bool ValidEnvironmentName(string value) => value.Length is > 0 and <= 100 &&
        (char.IsAsciiLetter(value[0]) || value[0] == '_') && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
}

public sealed class OciImagePolicy(string allowedImage)
{
    public void EnsureAllowed(string image)
    {
        if (!string.Equals(image, allowedImage, StringComparison.Ordinal)) throw new ArgumentException("Sandbox image is not allowlisted.");
    }
}
