using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio;

public sealed record PackageDiagnostic(string Code, string Severity, string Message, string? Path = null);
public sealed record PackageValidation(string PackageSha256, IReadOnlyList<PackageDiagnostic> Diagnostics);
public sealed record PackageImport(Guid ImportId, Guid ProblemId, string PackageSha256, string Mode);
public sealed record PackageExport(Guid ExportId, string Sha256, long Length, DateTimeOffset CreatedAtUtc, DateTimeOffset? ExpiresAtUtc);
public sealed record PackageImportMetadata(Guid ImportId, Guid ProblemId, string PackageSha256, string Mode, DateTimeOffset CreatedAtUtc);
public sealed record PackageExportMetadata(Guid ExportId, Guid ProblemId, int ProblemVersion, bool IncludeHidden, string PackageFormatVersion,
    string Sha256, long Length, DateTimeOffset CreatedAtUtc, DateTimeOffset? ExpiresAtUtc, string Status, string? FailureCode);

public interface IProblemPackageClient
{
    Task<PackageValidation> ValidateAsync(Stream package, CancellationToken cancellationToken);
    Task<PackageImport> CreateNewAsync(Stream package, string idempotencyKey, CancellationToken cancellationToken);
    Task<PackageImport> ReplaceDraftAsync(Guid problemId, int expectedVersion, Stream package, string idempotencyKey, CancellationToken cancellationToken);
    Task<PackageExport> ExportAsync(Guid problemId, string idempotencyKey, CancellationToken cancellationToken);
    Task<Stream> DownloadAsync(Guid problemId, Guid exportId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PackageImportMetadata>> ListImportsAsync(Guid problemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PackageExportMetadata>> ListExportsAsync(Guid problemId, CancellationToken cancellationToken);
    Task ExpireAsync(Guid problemId, Guid exportId, CancellationToken cancellationToken);
}

public sealed class ProblemPackageClient(IApiTransport transport) : IProblemPackageClient
{
    private const string ContentType = "application/vnd.mastemis.problem+zip";
    public async Task<PackageValidation> ValidateAsync(Stream package, CancellationToken cancellationToken)
        => await transport.UploadAsync<PackageValidation>(HttpMethod.Post, "/api/problem-studio/packages/validate", package, ContentType, null, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The server returned no package validation result.");
    public async Task<PackageImport> CreateNewAsync(Stream package, string idempotencyKey, CancellationToken cancellationToken)
        => await transport.UploadAsync<PackageImport>(HttpMethod.Post, "/api/problem-studio/packages/import", package, ContentType, idempotencyKey, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The server returned no package import result.");
    public async Task<PackageImport> ReplaceDraftAsync(Guid problemId, int expectedVersion, Stream package, string idempotencyKey, CancellationToken cancellationToken)
        => await transport.UploadAsync<PackageImport>(HttpMethod.Put, $"/api/problem-studio/drafts/{problemId:D}/packages/import?expectedVersion={expectedVersion}", package, ContentType, idempotencyKey, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The server returned no package replacement result.");
    public async Task<PackageExport> ExportAsync(Guid problemId, string idempotencyKey, CancellationToken cancellationToken)
        => await transport.SendAsync<object, PackageExport>(HttpMethod.Post, $"/api/problem-studio/drafts/{problemId:D}/packages/export", new { }, idempotencyKey, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The server returned no package export result.");
    public Task<Stream> DownloadAsync(Guid problemId, Guid exportId, CancellationToken cancellationToken)
        => transport.DownloadAsync($"/api/problem-studio/drafts/{problemId:D}/packages/exports/{exportId:D}", cancellationToken);
    public async Task<IReadOnlyList<PackageImportMetadata>> ListImportsAsync(Guid id, CancellationToken ct) =>
        await transport.GetAsync<List<PackageImportMetadata>>($"/api/problem-studio/drafts/{id:D}/packages/imports", ct).ConfigureAwait(false) ?? [];
    public async Task<IReadOnlyList<PackageExportMetadata>> ListExportsAsync(Guid id, CancellationToken ct) =>
        await transport.GetAsync<List<PackageExportMetadata>>($"/api/problem-studio/drafts/{id:D}/packages/exports", ct).ConfigureAwait(false) ?? [];
    public Task ExpireAsync(Guid id, Guid exportId, CancellationToken ct) => transport.SendAsync(HttpMethod.Delete,
        $"/api/problem-studio/drafts/{id:D}/packages/exports/{exportId:D}", new { }, null, ct);
}
