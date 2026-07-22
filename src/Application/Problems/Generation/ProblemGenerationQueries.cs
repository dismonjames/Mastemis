using Mastemis.Domain;

namespace Mastemis.Application.Problems.Generation;

public sealed record GenerationProgress(
    Guid OperationId,
    ProblemId ProblemId,
    string Status,
    int Numerator,
    int Denominator,
    int GeneratedInputs,
    int ExpectedOutputs,
    Guid? PublishedTestSetId,
    string? ReferenceJobStatus,
    DateTimeOffset UpdatedAtUtc);

public sealed record GenerationDiagnostic(string Code, string Message);

public interface IProblemGenerationQueryStore
{
    Task<GenerationProgress?> GetProgressAsync(
        ProblemId problemId,
        Guid operationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GenerationDiagnostic>> GetDiagnosticsAsync(
        ProblemId problemId,
        Guid operationId,
        int offset,
        int limit,
        CancellationToken cancellationToken);
}

public sealed class ProblemGenerationQueryService(
    IProblemGenerationQueryStore store,
    IAuthorizationService authorization)
{
    public async Task<GenerationProgress> GetProgressAsync(
        ProblemId problemId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("problem.read", problemId.Value, cancellationToken);
        return await store.GetProgressAsync(problemId, operationId, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Generation operation not found.");
    }

    public async Task<IReadOnlyList<GenerationDiagnostic>> GetDiagnosticsAsync(
        ProblemId problemId,
        Guid operationId,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("problem.read", problemId.Value, cancellationToken);
        if (offset < 0 || limit is < 1 or > 100)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Diagnostic page is invalid.");
        return await store.GetDiagnosticsAsync(problemId, operationId, offset, limit, cancellationToken);
    }
}
