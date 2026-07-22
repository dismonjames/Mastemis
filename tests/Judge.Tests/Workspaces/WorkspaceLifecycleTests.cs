using Mastemis.Contracts.Judge;
using Mastemis.Judge.Workspaces;

namespace Mastemis.Judge.Tests.Workspaces;

public sealed class WorkspaceLifecycleTests
{
    [Theory]
    [InlineData("../main.cpp")]
    [InlineData("/tmp/main.cpp")]
    [InlineData("folder/main.cpp")]
    [InlineData("main\\evil.cpp")]
    [InlineData("máin.cpp")]
    public async Task Rejects_unsafe_candidate_names(string name)
    {
        await using var fixture = await WorkspaceFixture.CreateAsync();
        await Assert.ThrowsAsync<JudgeContractException>(async () => await fixture.Workspace.MaterializeSourcesAsync(
            [new(name, "int main(){}"u8.ToArray())], new HashSet<string> { ".cpp" }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Maps_candidate_names_to_generated_internal_files_and_cleans_workspace()
    {
        var fixture = await WorkspaceFixture.CreateAsync(); var root = fixture.Workspace.Root;
        var files = await fixture.Workspace.MaterializeSourcesAsync([new("main.cpp", "int main(){}"u8.ToArray())],
            new HashSet<string> { ".cpp" }, TestContext.Current.CancellationToken);
        Assert.EndsWith("source_001.cpp", files[0].InternalPath, StringComparison.Ordinal);
        await fixture.DisposeAsync(); Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task Duplicate_normalized_names_and_precreated_symlink_are_rejected()
    {
        await using var fixture = await WorkspaceFixture.CreateAsync();
        await Assert.ThrowsAsync<JudgeContractException>(async () => await fixture.Workspace.MaterializeSourcesAsync(
            [new("Main.cpp", new byte[] { 1 }), new("main.cpp", new byte[] { 2 })], new HashSet<string> { ".cpp" }, TestContext.Current.CancellationToken));
        if (!OperatingSystem.IsWindows())
        {
            var target = Path.Combine(fixture.Workspace.SourceDirectory, "source_001.cpp");
            File.CreateSymbolicLink(target, "/tmp");
            await Assert.ThrowsAnyAsync<IOException>(async () => await fixture.Workspace.MaterializeSourcesAsync(
                [new("safe.cpp", new byte[] { 1 })], new HashSet<string> { ".cpp" }, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Stale_reconciliation_is_bounded_and_cancellable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mastemis-workspace-{Guid.NewGuid():N}");
        try
        {
            var manager = new JudgeWorkspaceManager(root); var workspace = await manager.CreateAsync(TestContext.Current.CancellationToken);
            Directory.SetLastWriteTimeUtc(workspace.Root, DateTime.UtcNow.AddDays(-2));
            Assert.Equal(1, await manager.ReconcileStaleAsync(TimeSpan.FromDays(1), 1, TestContext.Current.CancellationToken));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await manager.ReconcileStaleAsync(TimeSpan.FromDays(1), 1, cancelled.Token));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class WorkspaceFixture : IAsyncDisposable
    {
        private WorkspaceFixture(string root, JudgeWorkspace workspace) { Root = root; Workspace = workspace; }
        public string Root { get; }
        public JudgeWorkspace Workspace { get; }
        public static async Task<WorkspaceFixture> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), $"mastemis-workspace-{Guid.NewGuid():N}");
            return new(root, await new JudgeWorkspaceManager(root).CreateAsync(TestContext.Current.CancellationToken));
        }
        public async ValueTask DisposeAsync() { await Workspace.DisposeAsync(); if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
