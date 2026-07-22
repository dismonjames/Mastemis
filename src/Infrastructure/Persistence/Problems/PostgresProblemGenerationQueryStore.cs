using Mastemis.Application;
using Mastemis.Application.Problems.Authoring;
using Mastemis.Application.Problems.Generation;
using Mastemis.Contracts.Problems.ReferenceOutputs;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class PostgresProblemGenerationQueryStore(MastemisDbContext db) : IProblemGenerationQueryStore
{
    public async Task<GenerationProgress?> GetProgressAsync(
        ProblemId problemId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        var operation = await db.ProblemGenerationOperations.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == operationId && x.ProblemId == problemId.Value,
                cancellationToken);
        if (operation is null)
            return null;

        var jobStatus = await db.ReferenceOutputJobs.AsNoTracking()
            .Where(x => x.OperationId == operationId)
            .Select(x => (ReferenceOutputJobStatus?)x.Status)
            .SingleOrDefaultAsync(cancellationToken);

        return new(
            operation.Id,
            problemId,
            ((GenerationOperationStatus)operation.Status).ToString(),
            operation.ProgressNumerator,
            operation.ProgressDenominator,
            operation.GeneratedInputCount,
            operation.ExpectedOutputCount,
            operation.PublishedTestSetId,
            jobStatus?.ToString(),
            operation.UpdatedAtUtc);
    }

    public async Task<IReadOnlyList<GenerationDiagnostic>> GetDiagnosticsAsync(
        ProblemId problemId,
        Guid operationId,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var operation = await db.ProblemGenerationOperations.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == operationId && x.ProblemId == problemId.Value,
                cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Generation operation not found.");

        var diagnostics = new List<GenerationDiagnostic>(2);
        if (!string.IsNullOrWhiteSpace(operation.FailureCode))
            diagnostics.Add(new(operation.FailureCode, "Generation failed with a stable failure code."));
        if (!string.IsNullOrWhiteSpace(operation.DiagnosticSummary))
            diagnostics.Add(new("problem.generation.diagnostic", operation.DiagnosticSummary));
        return diagnostics.Skip(offset).Take(limit).ToArray();
    }
}
