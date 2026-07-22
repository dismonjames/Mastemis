namespace Mastemis.Application.Problems.ReferenceOutputs;

public interface IReferenceOutputJobScheduler
{
    Task<Guid> ScheduleAsync(Guid operationId, CancellationToken cancellationToken);
}
