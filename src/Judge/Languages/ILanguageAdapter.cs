using Mastemis.Contracts.Judge;

namespace Mastemis.Judge.Languages;

public interface ILanguageAdapter
{
    string LanguageId { get; }
    IReadOnlySet<string> SourceExtensions { get; }
    ValueTask<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken cancellationToken);
    ValueTask<ExecutionPlan> CreateExecutionPlanAsync(CompiledArtifact artifact,
        RuntimeEnvironment environment, CancellationToken cancellationToken);
}
