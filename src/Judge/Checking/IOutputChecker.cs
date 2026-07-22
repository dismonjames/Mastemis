using Mastemis.Contracts.Judge;

namespace Mastemis.Judge.Checking;

public interface IOutputChecker
{
    string CheckerId { get; }
    ValueTask<CheckerResult> CheckAsync(CheckerRequest request, CancellationToken cancellationToken);
}
