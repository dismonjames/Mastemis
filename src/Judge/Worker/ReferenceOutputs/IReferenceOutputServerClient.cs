using Mastemis.Contracts.Problems.ReferenceOutputs;

namespace Mastemis.Judge.Worker.ReferenceOutputs;

public interface IReferenceOutputServerClient
{
    Task<ReferenceOutputJobLease?> ClaimAsync(int leaseSeconds, CancellationToken cancellationToken);
    Task<ReferenceOutputJobPayload> GetPayloadAsync(Guid jobId, Guid leaseToken, CancellationToken cancellationToken);
    Task<byte[]> GetSourceAsync(Guid jobId, Guid leaseToken, string fileName, long maximumBytes, CancellationToken cancellationToken);
    Task<byte[]> GetInputAsync(Guid jobId, Guid leaseToken, int testIndex, long maximumBytes, CancellationToken cancellationToken);
    Task StartAsync(Guid jobId, Guid leaseToken, CancellationToken cancellationToken);
    Task RenewAsync(Guid jobId, Guid leaseToken, int leaseSeconds, CancellationToken cancellationToken);
    Task UploadAsync(Guid jobId, ReferenceOutputUploadMetadata metadata, ReadOnlyMemory<byte> output, CancellationToken cancellationToken);
    Task CompleteAsync(Guid jobId, ReferenceOutputCompletion completion, CancellationToken cancellationToken);
    Task FailAsync(Guid jobId, ReferenceOutputFailure failure, CancellationToken cancellationToken);
}

public sealed record ReferenceOutputUploadMetadata(Guid OperationId, Guid LeaseToken, int ContractVersion, int TestIndex,
    string Sha256, long Length, long ExecutionMilliseconds, long? PeakMemoryBytes, string SandboxBackend, string JudgeVersion);
