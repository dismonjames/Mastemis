namespace Mastemis.Application.Problems.Overview;

public sealed record ProblemOverview(Guid ProblemId, string Title, int DraftRevision, string Permission,
    bool ActiveExaminationLocked, int LocaleCount, IReadOnlyList<string> AcceptedLanguages,
    long TimeLimitMilliseconds, long MemoryLimitBytes, long OutputLimitBytes, string Checker,
    string MasValidationStatus, string MasRuntimeVersion, Guid? ReferenceRevisionId, string ReferenceValidationStatus,
    Guid? ActiveGenerationId, string? GenerationStatus, int? PublishedTestSetVersion, int GroupCount,
    int TestCount, int HiddenTestCount, DateTimeOffset? LatestImportUtc, DateTimeOffset? LatestExportUtc);

public interface IProblemOverviewStore
{
    Task<ProblemOverview?> GetAsync(Guid problemId, CancellationToken cancellationToken);
}

public sealed class ProblemOverviewService(IProblemOverviewStore store, IAuthorizationService authorization)
{
    public async Task<ProblemOverview> GetAsync(Guid problemId, CancellationToken ct)
    {
        await authorization.EnsureAsync("problem.read", problemId, ct);
        return await store.GetAsync(problemId, ct) ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem was not found.");
    }
}
