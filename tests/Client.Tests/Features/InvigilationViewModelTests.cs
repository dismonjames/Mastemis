using Mastemis.Client.Core.Features.Invigilation;
using Mastemis.Client.Core.Networking.Realtime;
using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Tests.Features;

public sealed class InvigilationViewModelTests
{
    [Fact]
    public void Invalid_examination_identifier_is_rejected_before_connection()
    {
        var viewModel = new InvigilationViewModel(new RealtimeClient(new ClientSession(), new ImmediateUiDispatcher()))
        {
            ExaminationId = "not-an-id"
        };

        viewModel.SubscribeCommand.Execute(null);

        Assert.Equal("Enter a valid examination identifier.", viewModel.Error);
    }
}
