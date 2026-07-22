using Mastemis.Application.Problems.Statements;
using Mastemis.Domain;

namespace Mastemis.Application.Tests.Problems;

public sealed class ProblemStatementServiceTests
{
    [Fact]
    public async Task Save_requires_mutation_authorization_and_validates_bounds()
    {
        var authorization = new Authorization(); var store = new Store(); var service = new ProblemStatementService(store, authorization);
        var id = ProblemId.New(); var content = new ProblemStatementContent("Title", "Statement", "Input", "Output", "n <= 10", "");
        var saved = await service.SaveAsync(id, "en", content, null, TestContext.Current.CancellationToken);
        Assert.Equal(1, saved.Revision); Assert.Equal(("problem.manage", id.Value), authorization.Last);
        await Assert.ThrowsAsync<ApplicationFailure>(() => service.SaveAsync(id, "en", content with { Markdown = new string('x', 1_000_001) }, 1, TestContext.Current.CancellationToken));
    }

    private sealed class Authorization : IAuthorizationService
    {
        public (string, Guid) Last { get; private set; }
        public ValueTask EnsureAsync(string permission, Guid scopeId, CancellationToken cancellationToken) { Last = (permission, scopeId); return ValueTask.CompletedTask; }
    }
    private sealed class Store : IProblemStatementStore
    {
        private ProblemStatement? _value;
        public Task<IReadOnlyList<ProblemStatementSummary>> ListAsync(ProblemId problemId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProblemStatementSummary>>([]);
        public Task<ProblemStatement?> GetAsync(ProblemId problemId, string locale, CancellationToken cancellationToken) => Task.FromResult(_value);
        public Task<ProblemStatement> SaveAsync(ProblemId problemId, string locale, ProblemStatementContent content, int? expectedRevision, CancellationToken cancellationToken)
        { _value = new(problemId, locale, content, 1, "hash", 1, UserId.New(), DateTimeOffset.UtcNow); return Task.FromResult(_value); }
        public Task DeleteAsync(ProblemId problemId, string locale, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
