namespace Mastemis.Sandbox.Contracts;

public sealed record SandboxResourceLimits(TimeSpan CpuTime, TimeSpan WallTime, long MemoryBytes,
    long OutputBytes, long FileBytes, int ProcessCount);

public sealed record SandboxRequest(
    string Image,
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkspacePath,
    string? StandardInputPath,
    string StandardOutputPath,
    string StandardErrorPath,
    IReadOnlyDictionary<string, string> Environment,
    SandboxResourceLimits Limits,
    bool NetworkDisabled = true);

public sealed record SandboxResult(
    SandboxExitKind ExitKind,
    int? ExitCode,
    int? Signal,
    TimeSpan CpuTime,
    TimeSpan WallTime,
    long? PeakMemoryBytes,
    long StandardOutputBytes,
    long StandardErrorBytes,
    SandboxResourceViolation? ResourceViolation,
    string? InfrastructureDiagnostic,
    string Backend);

public sealed record SandboxCapabilities(bool Available, string Backend, string? Version, bool Rootless,
    bool Cgroups, bool MemoryLimit, bool ProcessLimit, bool NetworkIsolation, bool ReadOnlyFilesystem,
    bool ResourceMeasurement, string? UnavailableReason)
{
    public bool MeetsMandatoryRequirements => Available && Cgroups && MemoryLimit && ProcessLimit &&
        NetworkIsolation && ReadOnlyFilesystem;
}

public enum SandboxExitKind { Exited, Signalled, TimedOut, Cancelled, ResourceLimit, InfrastructureFailure }
public enum SandboxResourceViolation { CpuTime, WallTime, Memory, Output, FileSize, ProcessCount }
