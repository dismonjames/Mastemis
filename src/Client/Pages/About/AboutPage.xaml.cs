using Mastemis.Client.Core.Features.About;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;

namespace Mastemis.Client.Pages.About;

public sealed partial class AboutPage : Page, IClientPage
{
    public AboutPage(AboutViewModel viewModel) { InitializeComponent(); DataContext = viewModel; }
    public ClientRoute Route => ClientRoute.About;
}
