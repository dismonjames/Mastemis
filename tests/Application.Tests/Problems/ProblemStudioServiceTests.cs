using Mastemis.Application.Problems.Authoring;
using Mastemis.Application.Problems.Generation;
using Mastemis.Domain;

namespace Mastemis.Application.Tests.Problems;

public sealed class ProblemStudioServiceTests
{
    [Fact]
    public async Task Preview_is_bounded_and_does_not_publish()
    {
        var store = new FakeStore(); var service = new ProblemStudioService(store, new Allow()); var id = ProblemId.New();
        var result = await service.PreviewAsync(id, "test 20 { input = int(1, 10) }", 4, 3, TestContext.Current.CancellationToken);
        Assert.True(result.Valid); Assert.Equal(3, result.Tests.Count); Assert.Empty(store.Published);
    }
    [Fact]
    public async Task Generation_publishes_complete_set_once()
    {
        var store = new FakeStore(); var service = new ProblemStudioService(store, new Allow()); var draft = await store.CreateAsync("X", "en", TestContext.Current.CancellationToken);
        await store.SaveMasAsync(draft.Id, "test 2 { input = int(1, 10) }", "hash", TestContext.Current.CancellationToken);
        var operation = await service.GenerateAsync(draft.Id, 2, TestContext.Current.CancellationToken);
        Assert.Equal(GenerationOperationStatus.Completed, operation.Status); Assert.Equal(2, store.Published.Count);
    }
    private sealed class Allow : IAuthorizationService { public ValueTask EnsureAsync(string permission, Guid scopeId, CancellationToken cancellationToken) => ValueTask.CompletedTask; }
    private sealed class FakeStore : IProblemStudioStore
    {
        private DraftProblem? _problem; private ProblemGenerationOperation? _operation;
        public List<byte[]> Published { get; } = [];
        public Task<DraftProblem> CreateAsync(string title, string locale, CancellationToken cancellationToken)
        { _problem = new(ProblemId.New(), title, locale, new Dictionary<string, string>(), 1000, 64 * 1024 * 1024, 1024, "exact", "", ""); return Task.FromResult(_problem); }
        public Task<DraftProblem?> GetAsync(ProblemId problemId, CancellationToken cancellationToken) => Task.FromResult(_problem);
        public Task SaveMasAsync(ProblemId problemId, string source, string sha256, CancellationToken cancellationToken) { _problem = _problem! with { MasSource = source, MasSha256 = sha256 }; return Task.CompletedTask; }
        public Task<ProblemGenerationOperation> BeginGenerationAsync(ProblemId problemId, ulong seed, string runtimeVersion, CancellationToken cancellationToken)
        { _operation = new(Guid.NewGuid(), problemId, GenerationOperationStatus.Running, seed, runtimeVersion, DateTimeOffset.UtcNow, null, null); return Task.FromResult(_operation); }
        public Task PublishTestsAsync(ProblemGenerationOperation operation, IReadOnlyList<(int Index, string Group, byte[] Input, string Hash)> tests, CancellationToken cancellationToken)
        { Published.AddRange(tests.Select(x => x.Input)); _operation = operation with { Status = GenerationOperationStatus.Completed, CompletedAtUtc = DateTimeOffset.UtcNow }; return Task.CompletedTask; }
        public Task FailGenerationAsync(Guid operationId, string failureCode, CancellationToken cancellationToken) { _operation = _operation! with { Status = GenerationOperationStatus.Failed, FailureCode = failureCode }; return Task.CompletedTask; }
        public Task CancelGenerationAsync(Guid operationId, CancellationToken cancellationToken) { _operation = _operation! with { Status = GenerationOperationStatus.Cancelled }; return Task.CompletedTask; }
        public Task<ProblemGenerationOperation?> GetGenerationAsync(Guid operationId, CancellationToken cancellationToken) => Task.FromResult(_operation);
    }
}
