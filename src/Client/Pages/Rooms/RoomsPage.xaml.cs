using Mastemis.Client.Core.Features.Rooms;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;

namespace Mastemis.Client.Pages.Rooms;

public sealed partial class RoomsPage : Page, IClientPage
{
    public RoomsPage(RoomOperationsViewModel viewModel) { InitializeComponent(); DataContext = viewModel; }
    public ClientRoute Route => ClientRoute.Rooms;
}
