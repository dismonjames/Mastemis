using Mastemis.Client.Core.Features.CandidateExam;

namespace Mastemis.Client.Tests.Features;

public sealed class CandidateWorkspaceTests
{
    [Fact]
    public async Task TerminatedSessionLocksEditorState()
    {
        var model = new CandidateWorkspaceViewModel(new Client());
        model.SessionId = Guid.NewGuid().ToString();
        model.LoadCommand.Execute(null);
        for (var i = 0; i < 100 && model.Session is null; i++) await Task.Delay(2, TestContext.Current.CancellationToken);
        Assert.True(model.IsLocked);
        Assert.Equal("Terminated", model.SessionState);
    }

    private sealed class Client : ICandidateSessionClient
    {
        public Task<CandidateSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken) => Task.FromResult<CandidateSession?>(new(sessionId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Terminated", 3, Guid.NewGuid()));
        public Task<DraftRevision> SaveDraftAsync(Guid sessionId, string source, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<IReadOnlyList<SubmissionItem>> ListSubmissionsAsync(Guid sessionId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SubmissionItem>>([]);
        public Task<SubmissionItem> SubmitAsync(Guid sessionId, Guid problemId, Guid revisionId, string language, CancellationToken cancellationToken) => throw new InvalidOperationException();
    }
}
