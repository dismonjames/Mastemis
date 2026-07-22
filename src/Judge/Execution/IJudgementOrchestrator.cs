using Mastemis.Contracts.Judge;

namespace Mastemis.Judge.Execution;

public interface IJudgementOrchestrator
{
    ValueTask<JudgeExecutionResult> ExecuteAsync(JudgeExecutionRequest request, CancellationToken cancellationToken);
}
