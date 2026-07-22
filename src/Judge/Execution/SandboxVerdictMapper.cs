using Mastemis.Contracts.Judge;
using Mastemis.Domain;
using Mastemis.Sandbox.Contracts;

namespace Mastemis.Judge.Execution;

public static class SandboxVerdictMapper
{
    public static (SubmissionState? Verdict, JudgeFailureCode? Failure) Map(SandboxResult result)
    {
        if (result.ExitKind == SandboxExitKind.InfrastructureFailure) return (SubmissionState.InfrastructureError, JudgeFailureCode.SandboxFailure);
        if (result.ExitKind == SandboxExitKind.Cancelled) return (SubmissionState.Cancelled, JudgeFailureCode.Cancelled);
        if (result.ExitKind == SandboxExitKind.TimedOut || result.ResourceViolation is SandboxResourceViolation.CpuTime or SandboxResourceViolation.WallTime)
            return (SubmissionState.TimeLimitExceeded, JudgeFailureCode.TimeLimit);
        if (result.ResourceViolation == SandboxResourceViolation.Memory) return (SubmissionState.MemoryLimitExceeded, JudgeFailureCode.MemoryLimit);
        if (result.ResourceViolation == SandboxResourceViolation.Output) return (SubmissionState.OutputLimitExceeded, JudgeFailureCode.OutputLimit);
        if (result.ResourceViolation is SandboxResourceViolation.FileSize or SandboxResourceViolation.ProcessCount)
            return (SubmissionState.RuntimeError, result.ResourceViolation == SandboxResourceViolation.FileSize ? JudgeFailureCode.FileLimit : JudgeFailureCode.ProcessLimit);
        if (result.ExitKind is SandboxExitKind.Signalled or SandboxExitKind.ResourceLimit || result.ExitCode != 0)
            return (SubmissionState.RuntimeError, JudgeFailureCode.RuntimeFailure);
        return (null, null);
    }
}
