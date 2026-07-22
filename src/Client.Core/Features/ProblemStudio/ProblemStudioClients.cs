using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio;

public sealed record ProblemDraftSummary(Guid Id, string Title, string DefaultLocale, long TimeLimitMilliseconds, long MemoryLimitBytes, long OutputLimitBytes, string Checker, string MasSha256, int Version = 0);
public sealed record MasDocument(string Source, string Sha256, int Revision, string RuntimeVersion);
public sealed record MasDiagnostic(string Code, string Severity, string Message, int? Line = null, int? Column = null);
public sealed record GenerationStatus(Guid Id, Guid ProblemId, string Status, int ProgressNumerator, int ProgressDenominator, string? FailureCode, Guid? PublishedTestSetId);
public sealed record GenerationProgress(Guid OperationId, string Status, int Numerator, int Denominator, int GeneratedInputs,
    int ExpectedOutputs, Guid? PublishedTestSetId, string? ReferenceJobStatus, DateTimeOffset UpdatedAtUtc);
public sealed record GenerationDiagnostic(string Code, string Message);

public interface IProblemDraftClient
{
    Task<IReadOnlyList<ProblemDraftSummary>> ListAsync(CancellationToken cancellationToken);
    Task<ProblemDraftSummary?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ProblemDraftSummary> CreateAsync(string title, string locale, CancellationToken cancellationToken);
}

public sealed class ProblemDraftClient(IApiTransport transport) : IProblemDraftClient
{
    public async Task<IReadOnlyList<ProblemDraftSummary>> ListAsync(CancellationToken cancellationToken)
        => await transport.GetAsync<List<ProblemDraftSummary>>("/api/problem-studio/drafts", cancellationToken).ConfigureAwait(false) ?? [];
    public Task<ProblemDraftSummary?> GetAsync(Guid id, CancellationToken cancellationToken)
        => transport.GetAsync<ProblemDraftSummary>($"/api/problem-studio/drafts/{id:D}", cancellationToken);
    public async Task<ProblemDraftSummary> CreateAsync(string title, string locale, CancellationToken cancellationToken)
        => await transport.SendAsync<object, ProblemDraftSummary>(HttpMethod.Post, "/api/problem-studio/drafts", new { title, defaultLocale = locale }, Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The server returned an empty draft.");
}

public interface IProblemMasClient
{
    Task<MasDocument?> GetAsync(Guid problemId, CancellationToken cancellationToken);
    Task<MasDocument?> UpdateAsync(Guid problemId, string source, int expectedRevision, CancellationToken cancellationToken);
    Task<IReadOnlyList<MasDiagnostic>> ValidateAsync(Guid problemId, string source, CancellationToken cancellationToken);
}

public sealed class ProblemMasClient(IApiTransport transport) : IProblemMasClient
{
    public Task<MasDocument?> GetAsync(Guid problemId, CancellationToken cancellationToken) => transport.GetAsync<MasDocument>($"/api/problem-studio/drafts/{problemId:D}/mas", cancellationToken);
    public Task<MasDocument?> UpdateAsync(Guid problemId, string source, int expectedRevision, CancellationToken cancellationToken)
        => transport.SendAsync<object, MasDocument>(HttpMethod.Put, $"/api/problem-studio/drafts/{problemId:D}/mas", new { source, expectedRevision }, Guid.NewGuid().ToString("N"), cancellationToken);
    public async Task<IReadOnlyList<MasDiagnostic>> ValidateAsync(Guid problemId, string source, CancellationToken cancellationToken)
        => await transport.SendAsync<object, List<MasDiagnostic>>(HttpMethod.Post, $"/api/problem-studio/drafts/{problemId:D}/mas/validate", new { source }, null, cancellationToken).ConfigureAwait(false) ?? [];
}

public interface IProblemGenerationClient
{
    Task<GenerationStatus> StartAsync(Guid problemId, ulong seed, CancellationToken cancellationToken);
    Task<GenerationStatus?> GetAsync(Guid problemId, Guid operationId, CancellationToken cancellationToken);
    Task<GenerationProgress?> GetProgressAsync(Guid problemId, Guid operationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GenerationDiagnostic>> GetDiagnosticsAsync(Guid problemId, Guid operationId, CancellationToken cancellationToken);
    Task CancelAsync(Guid problemId, Guid operationId, CancellationToken cancellationToken);
}

public sealed class ProblemGenerationClient(IApiTransport transport) : IProblemGenerationClient
{
    public async Task<GenerationStatus> StartAsync(Guid problemId, ulong seed, CancellationToken cancellationToken)
        => await transport.SendAsync<object, GenerationStatus>(HttpMethod.Post, $"/api/problem-studio/drafts/{problemId:D}/generation", new { seed }, Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The server returned an empty generation operation.");
    public Task<GenerationStatus?> GetAsync(Guid problemId, Guid operationId, CancellationToken cancellationToken)
        => transport.GetAsync<GenerationStatus>($"/api/problem-studio/drafts/{problemId:D}/generation/{operationId:D}", cancellationToken);
    public Task<GenerationProgress?> GetProgressAsync(Guid problemId, Guid operationId, CancellationToken cancellationToken)
        => transport.GetAsync<GenerationProgress>($"/api/problem-studio/drafts/{problemId:D}/generation/{operationId:D}/progress", cancellationToken);
    public async Task<IReadOnlyList<GenerationDiagnostic>> GetDiagnosticsAsync(Guid problemId, Guid operationId, CancellationToken cancellationToken)
        => await transport.GetAsync<List<GenerationDiagnostic>>($"/api/problem-studio/drafts/{problemId:D}/generation/{operationId:D}/diagnostics?offset=0&limit=50", cancellationToken).ConfigureAwait(false) ?? [];
    public Task CancelAsync(Guid problemId, Guid operationId, CancellationToken cancellationToken)
        => transport.SendAsync(HttpMethod.Delete, $"/api/problem-studio/drafts/{problemId:D}/generation/{operationId:D}", new { }, null, cancellationToken);
}
