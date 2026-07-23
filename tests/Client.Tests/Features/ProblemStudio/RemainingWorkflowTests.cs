using Mastemis.Client.Core.Features.ProblemStudio;
using Mastemis.Client.Core.Features.ProblemStudio.Packages;
using Mastemis.Client.Core.Features.ProblemStudio.Permissions;
using Mastemis.Client.Core.Platform.Files;

namespace Mastemis.Client.Tests.Features.ProblemStudio;

public sealed class RemainingWorkflowTests
{
    [Fact]
    public async Task CreateNew_reports_real_stream_progress_and_created_problem()
    {
        var bytes = new byte[4096];
        var files = new FileStub(new("sample.mas", "application/vnd.mastemis.problem+zip", bytes.Length,
            _ => Task.FromResult<Stream>(new MemoryStream(bytes))));
        var model = new ProblemPackageViewModel(new PackageStub(), files);
        await model.AcceptDroppedAsync(new object(), CancellationToken.None);

        model.CreateNewCommand.Execute(null);
        await WaitUntilAsync(() => model.Status.StartsWith("Created problem", StringComparison.Ordinal));

        Assert.Equal(bytes.Length, model.TransferredBytes);
        Assert.False(model.IsTransferring);
    }

    [Fact]
    public async Task Permission_expiration_is_converted_to_utc()
    {
        var client = new PermissionStub();
        var model = new ProblemPermissionViewModel(client) { UserId = Guid.NewGuid().ToString(), Expiration = DateTimeOffset.Now.AddHours(2).ToString("O") };
        model.SetProblem(Guid.NewGuid());

        model.AssignCommand.Execute(null);
        await WaitUntilAsync(() => client.ExpiresAtUtc is not null);

        Assert.Equal(TimeSpan.Zero, client.ExpiresAtUtc?.Offset);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var index = 0; index < 100 && !condition(); index++) await Task.Delay(10);
        Assert.True(condition());
    }

    private sealed class FileStub(ClientFile file) : IClientFileService
    {
        public Task<ClientFile?> PickOpenAsync(IReadOnlyList<string> extensions, CancellationToken cancellationToken) => Task.FromResult<ClientFile?>(file);
        public Task<ClientFile?> OpenDroppedAsync(object platformFile, IReadOnlyList<string> extensions, CancellationToken cancellationToken) => Task.FromResult<ClientFile?>(file);
        public Task SaveAsync(string suggestedName, Stream content, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class PackageStub : IProblemPackageClient
    {
        public Task<PackageValidation> ValidateAsync(Stream package, CancellationToken cancellationToken) => Task.FromResult(new PackageValidation("hash", []));
        public async Task<PackageImport> CreateNewAsync(Stream package, string idempotencyKey, CancellationToken cancellationToken) { await package.CopyToAsync(Stream.Null, cancellationToken); return new(Guid.NewGuid(), Guid.NewGuid(), "hash", "CreateNew"); }
        public Task<PackageImport> ReplaceDraftAsync(Guid problemId, int expectedVersion, Stream package, string idempotencyKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PackageExport> ExportAsync(Guid problemId, string idempotencyKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Stream> DownloadAsync(Guid problemId, Guid exportId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<PackageImportMetadata>> ListImportsAsync(Guid problemId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PackageImportMetadata>>([]);
        public Task<IReadOnlyList<PackageExportMetadata>> ListExportsAsync(Guid problemId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PackageExportMetadata>>([]);
        public Task ExpireAsync(Guid problemId, Guid exportId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class PermissionStub : IProblemPermissionClient
    {
        public DateTimeOffset? ExpiresAtUtc { get; private set; }
        public Task<IReadOnlyList<ProblemPermissionItem>> ListAsync(Guid problemId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProblemPermissionItem>>([]);
        public Task<ProblemPermissionItem?> AssignAsync(Guid problemId, Guid userId, string role, DateTimeOffset? expires, CancellationToken cancellationToken) { ExpiresAtUtc = expires; return Task.FromResult<ProblemPermissionItem?>(null); }
        public Task RevokeAsync(Guid problemId, Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ProblemExamAssignmentItem>> ListExamsAsync(Guid problemId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProblemExamAssignmentItem>>([]);
        public Task AssignExamAsync(Guid problemId, Guid examId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RemoveExamAsync(Guid problemId, Guid examId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
