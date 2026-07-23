using Mastemis.Client.Core.Features.ProblemStudio.Tests;

namespace Mastemis.Client.Tests.Features.ProblemStudio;

public sealed class ProblemTestViewModelTests
{
    [Fact]
    public void Selecting_a_problem_clears_previous_sensitive_preview()
    {
        var model = new ProblemTestViewModel(new TestClientStub());

        model.SetProblem(Guid.NewGuid());

        Assert.Empty(model.Items);
        Assert.Contains("Select a test", model.Preview, StringComparison.Ordinal);
    }

    private sealed class TestClientStub : IProblemTestClient
    {
        public Task<IReadOnlyList<ProblemTestItem>> ListAsync(Guid problemId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProblemTestItem>>([]);
        public Task<Stream> OpenInputAsync(Guid problemId, int index, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream());
        public Task<Stream> OpenOutputAsync(Guid problemId, int index, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream());
        public Task<IReadOnlyList<ProblemTestSetItem>> ListVersionsAsync(Guid problemId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProblemTestSetItem>>([]);
        public Task<ProblemTestPage?> ListPageAsync(Guid problemId, Guid testSetId, int offset, int limit, CancellationToken cancellationToken) => Task.FromResult<ProblemTestPage?>(null);
    }
}
