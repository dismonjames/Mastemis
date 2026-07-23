using Mastemis.Client.Core.Features.ProblemStudio.ReferenceSolution;

namespace Mastemis.Client.Tests.Features.ProblemStudio;

public sealed class ReferenceSolutionViewModelTests
{
    [Fact]
    public void Source_files_are_added_without_duplicate_logical_names()
    {
        var model = new ReferenceSolutionViewModel(new ReferenceClientStub());
        model.NewFileName = "main.cpp";

        model.AddFileCommand.Execute(null);
        model.AddFileCommand.Execute(null);

        Assert.Single(model.Sources);
        Assert.Equal("main.cpp", model.Selected?.FileName);
    }

    private sealed class ReferenceClientStub : IReferenceSolutionClient
    {
        public Task<ReferenceRevision?> GetAsync(Guid problemId, CancellationToken cancellationToken) => Task.FromResult<ReferenceRevision?>(null);
        public Task<ReferenceRevision?> SaveAsync(Guid problemId, string language, IReadOnlyList<ReferenceSourceUpdate> sources, CancellationToken cancellationToken) => Task.FromResult<ReferenceRevision?>(null);
        public Task<Stream> OpenSourceAsync(Guid problemId, Guid revisionId, string fileName, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream());
    }
}
