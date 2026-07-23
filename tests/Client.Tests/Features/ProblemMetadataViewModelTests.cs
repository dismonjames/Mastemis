using Mastemis.Client.Core.Features.ProblemStudio;
using Mastemis.Client.Core.Features.ProblemStudio.Metadata;

namespace Mastemis.Client.Tests.Features;

public sealed class ProblemMetadataViewModelTests
{
    [Fact]
    public async Task Editing_marks_dirty_and_save_uses_loaded_revision()
    {
        var client = new Stub(); var viewModel = new ProblemMetadataViewModel(client);
        viewModel.Load(new(Guid.NewGuid(), "Old", "en", 1000, 1024, 256, "exact", "", 4));
        viewModel.Title = "New";
        Assert.True(viewModel.IsDirty);
        await viewModel.SaveAsync(CancellationToken.None);
        Assert.Equal(4, client.ExpectedVersion);
        Assert.False(viewModel.IsDirty);
    }
    private sealed class Stub : IProblemDraftClient
    {
        public int ExpectedVersion { get; private set; }
        public Task<IReadOnlyList<ProblemDraftSummary>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ProblemDraftSummary>>([]);
        public Task<ProblemDraftSummary?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult<ProblemDraftSummary?>(null);
        public Task<ProblemDraftSummary> CreateAsync(string title, string locale, CancellationToken ct) => throw new NotSupportedException();
        public Task<ProblemDraftSummary?> UpdateAsync(Guid id, ProblemMetadataUpdate update, CancellationToken ct) { ExpectedVersion = update.ExpectedVersion; return Task.FromResult<ProblemDraftSummary?>(new(id, update.Title, update.DefaultLocale, update.TimeLimitMilliseconds, update.MemoryLimitBytes, update.OutputLimitBytes, update.Checker, "", update.ExpectedVersion + 1)); }
    }
}
