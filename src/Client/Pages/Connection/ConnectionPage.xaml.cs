using Mastemis.Client.Core.Features.Connection;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;

namespace Mastemis.Client.Pages.Connection;

public sealed partial class ConnectionPage : Page, IClientPage
{
    public ConnectionPage(ConnectionViewModel viewModel) { InitializeComponent(); DataContext = viewModel; }
    public ClientRoute Route => ClientRoute.Connection;
}
