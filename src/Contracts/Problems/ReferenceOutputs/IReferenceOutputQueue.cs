using Mastemis.Domain;

namespace Mastemis.Contracts.Problems.ReferenceOutputs;

public interface IReferenceOutputQueue
{
    Task<Guid> EnqueueAsync(ReferenceOutputJobPayload payload, int maximumAttempts, CancellationToken cancellationToken);
    Task<ReferenceOutputJobLease?> ClaimAsync(JudgeWorkerId workerId, TimeSpan leaseDuration, CancellationToken cancellationToken);
    Task RenewAsync(JudgeWorkerId workerId, Guid jobId, Guid leaseToken, TimeSpan leaseDuration, CancellationToken cancellationToken);
    Task StartAsync(JudgeWorkerId workerId, Guid jobId, Guid leaseToken, CancellationToken cancellationToken);
    Task<ReferenceOutputJobPayload> GetPayloadAsync(JudgeWorkerId workerId, Guid jobId, Guid leaseToken, CancellationToken cancellationToken);
    Task CompleteAsync(ReferenceOutputCompletion completion, CancellationToken cancellationToken);
    Task FailAsync(ReferenceOutputFailure failure, CancellationToken cancellationToken);
    Task CancelAsync(Guid operationId, CancellationToken cancellationToken);
}
