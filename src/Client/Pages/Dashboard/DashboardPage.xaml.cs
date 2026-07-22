using Mastemis.Client.Core.Features.Dashboard;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;
namespace Mastemis.Client.Pages.Dashboard;
public sealed partial class DashboardPage : Page, IClientPage { public DashboardPage(DashboardViewModel viewModel) { InitializeComponent(); DataContext = viewModel; } public ClientRoute Route => ClientRoute.Dashboard; }
