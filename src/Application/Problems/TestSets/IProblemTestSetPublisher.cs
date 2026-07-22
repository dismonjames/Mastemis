namespace Mastemis.Application.Problems.TestSets;

public interface IProblemTestSetPublisher
{
    Task<Guid> PublishAsync(Guid operationId, CancellationToken cancellationToken);
}
