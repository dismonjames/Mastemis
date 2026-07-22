using Mastemis.Domain;

namespace Mastemis.Judge.Configuration;

public sealed record JudgeWorkerOptions(
    Uri ServerUrl,
    JudgeWorkerId WorkerId,
    string Secret,
    int Capacity,
    TimeSpan ClaimInterval,
    TimeSpan HeartbeatInterval,
    TimeSpan LeaseDuration,
    TimeSpan LeaseRenewalInterval,
    TimeSpan ShutdownTimeout,
    string WorkspaceRoot)
{
    public void Validate()
    {
        if (!ServerUrl.IsAbsoluteUri || ServerUrl.Scheme is not ("https" or "http") || string.IsNullOrWhiteSpace(Secret) ||
            Secret.Length > 500 || Capacity is < 1 or > 128 || ClaimInterval < TimeSpan.FromMilliseconds(100) ||
            HeartbeatInterval < TimeSpan.FromSeconds(1) || LeaseDuration < TimeSpan.FromSeconds(10) || LeaseDuration > TimeSpan.FromMinutes(30) ||
            LeaseRenewalInterval <= TimeSpan.Zero || LeaseRenewalInterval >= LeaseDuration || ShutdownTimeout <= TimeSpan.Zero ||
            !Path.IsPathFullyQualified(WorkspaceRoot)) throw new ArgumentException("Judge worker configuration is invalid.");
    }
}
