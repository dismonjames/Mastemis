using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio.Overview;

public sealed record ProblemOverviewData(Guid ProblemId, string Title, int DraftRevision, string Permission,
    bool ActiveExaminationLocked, int LocaleCount, IReadOnlyList<string> AcceptedLanguages,
    long TimeLimitMilliseconds, long MemoryLimitBytes, long OutputLimitBytes, string Checker,
    string MasValidationStatus, string MasRuntimeVersion, Guid? ReferenceRevisionId, string ReferenceValidationStatus,
    Guid? ActiveGenerationId, string? GenerationStatus, int? PublishedTestSetVersion, int GroupCount,
    int TestCount, int HiddenTestCount, DateTimeOffset? LatestImportUtc, DateTimeOffset? LatestExportUtc);

public interface IProblemOverviewClient { Task<ProblemOverviewData?> GetAsync(Guid problemId, CancellationToken cancellationToken); }
public sealed class ProblemOverviewClient(IApiTransport transport) : IProblemOverviewClient
{
    public Task<ProblemOverviewData?> GetAsync(Guid id, CancellationToken ct) => transport.GetAsync<ProblemOverviewData>($"/api/problem-studio/drafts/{id:D}/overview", ct);
}
