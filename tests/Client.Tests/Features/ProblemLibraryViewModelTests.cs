using Mastemis.Client.Core.Features.Problems;
using Mastemis.Client.Core.Features.ProblemStudio;

namespace Mastemis.Client.Tests.Features;

public sealed class ProblemLibraryViewModelTests
{
    [Fact]
    public async Task Refresh_and_search_expose_only_authorized_matching_drafts()
    {
        var client = new DraftClientStub([
            Draft("Arrays"),
            Draft("Graph paths")]);
        var viewModel = new ProblemLibraryViewModel(client);

        viewModel.RefreshCommand.Execute(null);
        await WaitUntilAsync(() => viewModel.Problems.Count == 2);
        viewModel.Search = "graph";

        Assert.Single(viewModel.VisibleProblems);
        Assert.Equal("Graph paths", viewModel.VisibleProblems[0].Title);
    }

    [Fact]
    public async Task Create_rejects_blank_title_without_calling_server()
    {
        var client = new DraftClientStub([]);
        var viewModel = new ProblemLibraryViewModel(client) { NewTitle = "  " };

        viewModel.CreateCommand.Execute(null);
        await WaitUntilAsync(() => viewModel.HasError);

        Assert.Equal(0, client.CreateCalls);
        Assert.Equal("Enter a problem title.", viewModel.Error);
    }

    private static ProblemDraftSummary Draft(string title) => new(Guid.NewGuid(), title, "en", 1000, 268435456, 1048576, "exact", string.Empty);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++) await Task.Delay(10);
        Assert.True(condition());
    }

    private sealed class DraftClientStub(IReadOnlyList<ProblemDraftSummary> values) : IProblemDraftClient
    {
        public int CreateCalls { get; private set; }
        public Task<IReadOnlyList<ProblemDraftSummary>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(values);
        public Task<ProblemDraftSummary?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<ProblemDraftSummary?>(values.FirstOrDefault(x => x.Id == id));
        public Task<ProblemDraftSummary> CreateAsync(string title, string locale, CancellationToken cancellationToken)
        {
            CreateCalls++;
            return Task.FromResult(Draft(title));
        }
    }
}
