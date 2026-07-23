using Mastemis.Client.Core.Features.Invigilation;
using Mastemis.Client.Core.Networking.Realtime;
using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Tests.Features;

public sealed class InvigilationViewModelTests
{
    [Fact]
    public void Invalid_examination_identifier_is_rejected_before_connection()
    {
        var viewModel = new InvigilationViewModel(new RealtimeClient(new ClientSession(), new ImmediateUiDispatcher()), new InvigilationClientStub())
        {
            ExaminationId = "not-an-id"
        };

        viewModel.SubscribeCommand.Execute(null);

        Assert.Equal("Enter a valid examination identifier.", viewModel.Error);
    }

    [Fact]
    public void Candidate_filters_and_detail_are_deterministic()
    {
        var session = Guid.NewGuid();
        var model = new InvigilationViewModel(new RealtimeClient(new ClientSession(), new ImmediateUiDispatcher()), new InvigilationClientStub());
        model.ApplySnapshot(new(Guid.NewGuid(), "Exam", "Open", [],
            [new(Guid.NewGuid(), session, Guid.NewGuid(), "Alice", "Active", "Connected", 2, 2, 1, false, 0, DateTimeOffset.UtcNow)],
            [new(Guid.NewGuid(), session, Guid.NewGuid(), 1, "Warning", DateTimeOffset.UtcNow)],
            [new(Guid.NewGuid(), session, "FocusLost", "Confirmed", DateTimeOffset.UtcNow)], DateTimeOffset.UtcNow));

        model.WarningFilter = "With warnings";
        model.SelectedCandidate = model.VisibleCandidates.Single();

        Assert.Equal(1, model.VisibleCandidateCount);
        Assert.Single(model.CandidateWarnings);
        Assert.Single(model.CandidateEvents);
    }

    private sealed class InvigilationClientStub : IInvigilationClient
    {
        public Task<InvigilationSnapshot?> GetExamAsync(Guid examId, CancellationToken cancellationToken) => Task.FromResult<InvigilationSnapshot?>(null);
    }
}
