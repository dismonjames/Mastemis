using Mastemis.Application;
using Mastemis.Application.Problems.Assets;
using Mastemis.Contracts.Problems.ReferenceOutputs;
using Mastemis.Domain;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class ReferenceOutputPayloadService(IReferenceOutputQueue queue, IProblemObjectStorage objects)
{
    public Task<ReferenceOutputJobPayload> GetAsync(JudgeWorkerId worker, Guid jobId, Guid lease, CancellationToken ct) =>
        queue.GetPayloadAsync(worker, jobId, lease, ct);

    public async Task<Stream> OpenSourceAsync(JudgeWorkerId worker, Guid jobId, Guid lease, string fileName, CancellationToken ct)
    {
        var payload = await queue.GetPayloadAsync(worker, jobId, lease, ct);
        var source = payload.Sources.SingleOrDefault(x => x.FileName == fileName) ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Reference source not found.");
        return await objects.OpenReadAsync(source.ObjectId, source.Length, ct);
    }

    public async Task<Stream> OpenInputAsync(JudgeWorkerId worker, Guid jobId, Guid lease, int testIndex, CancellationToken ct)
    {
        var payload = await queue.GetPayloadAsync(worker, jobId, lease, ct);
        var test = payload.Tests.SingleOrDefault(x => x.TestIndex == testIndex) ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Reference test input not found.");
        return await objects.OpenStagedReadAsync(test.InputObjectId, test.InputLength, ct);
    }
}
