using Mastemis.Contracts.Judge;
using Mastemis.Domain;

namespace Mastemis.Judge.Worker;

public interface IJudgeServerClient
{
    Task HeartbeatAsync(WorkerHeartbeatContract heartbeat, CancellationToken cancellationToken);
    Task<WorkerLeaseContract?> ClaimAsync(int leaseSeconds, CancellationToken cancellationToken);
    Task<WorkerJudgeContract> GetContractAsync(JudgeJobId jobId, Guid leaseId, CancellationToken cancellationToken);
    Task<byte[]> GetSourceAsync(JudgeJobId jobId, Guid leaseId, long maximumBytes, CancellationToken cancellationToken);
    Task<byte[]> GetTestInputAsync(JudgeJobId jobId, Guid leaseId, int index, long maximumBytes, CancellationToken cancellationToken);
    Task<byte[]> GetExpectedOutputAsync(JudgeJobId jobId, Guid leaseId, int index, long maximumBytes, CancellationToken cancellationToken);
    Task StartAsync(JudgeJobId jobId, Guid leaseId, CancellationToken cancellationToken);
    Task RenewAsync(JudgeJobId jobId, WorkerLeaseRenewal renewal, CancellationToken cancellationToken);
    Task CompleteAsync(JudgeJobId jobId, WorkerJudgementReport report, CancellationToken cancellationToken);
    Task FailAsync(JudgeJobId jobId, WorkerFailureReport report, CancellationToken cancellationToken);
}
