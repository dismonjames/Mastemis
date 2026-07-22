using Mastemis.Client.Core.Features.Invigilation;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;

namespace Mastemis.Client.Pages.Invigilation;

public sealed partial class InvigilationPage : Page, IClientPage
{
    public InvigilationPage(InvigilationViewModel viewModel) { InitializeComponent(); DataContext = viewModel; }
    public ClientRoute Route => ClientRoute.Invigilation;
}
