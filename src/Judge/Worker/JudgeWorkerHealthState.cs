using Mastemis.Sandbox.Contracts;

namespace Mastemis.Judge.Worker;

public sealed record JudgeWorkerHealthSnapshot(bool ServerConnected, bool Authenticated, SandboxCapabilities? Sandbox,
    bool CppAvailable, bool DotnetAvailable, bool WorkspaceWritable, int ActiveJobs, int Capacity,
    DateTimeOffset? LastHeartbeatUtc, string? LastFailureCode)
{
    public bool Ready => ServerConnected && Authenticated && Sandbox?.MeetsMandatoryRequirements == true &&
        CppAvailable && DotnetAvailable && WorkspaceWritable;
}

public sealed class JudgeWorkerHealthState
{
    private readonly object _gate = new();
    private JudgeWorkerHealthSnapshot _snapshot = new(false, false, null, false, false, false, 0, 0, null, null);
    public JudgeWorkerHealthSnapshot Snapshot { get { lock (_gate) return _snapshot; } }
    public void Update(Func<JudgeWorkerHealthSnapshot, JudgeWorkerHealthSnapshot> update) { lock (_gate) _snapshot = update(_snapshot); }
}
