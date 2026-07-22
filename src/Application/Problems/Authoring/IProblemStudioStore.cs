using Mastemis.Domain;

namespace Mastemis.Application.Problems.Authoring;

public interface IProblemStudioStore
{
    Task<DraftProblem> CreateAsync(string title, string locale, CancellationToken cancellationToken);
    Task<DraftProblem?> GetAsync(ProblemId problemId, CancellationToken cancellationToken);
    Task SaveMasAsync(ProblemId problemId, string source, string sha256, CancellationToken cancellationToken);
    Task<ProblemGenerationOperation> BeginGenerationAsync(ProblemId problemId, ulong seed, string runtimeVersion, CancellationToken cancellationToken);
    Task PublishTestsAsync(ProblemGenerationOperation operation, IReadOnlyList<(int Index, string Group, byte[] Input, string Hash)> tests,
        CancellationToken cancellationToken);
    Task FailGenerationAsync(Guid operationId, string failureCode, CancellationToken cancellationToken);
    Task CancelGenerationAsync(Guid operationId, CancellationToken cancellationToken);
    Task<ProblemGenerationOperation?> GetGenerationAsync(Guid operationId, CancellationToken cancellationToken);
}
