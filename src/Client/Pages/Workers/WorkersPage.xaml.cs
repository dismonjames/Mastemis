using Mastemis.Client.Core.Features.Workers;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;

namespace Mastemis.Client.Pages.Workers;

public sealed partial class WorkersPage : Page, IClientPage
{
    public WorkersPage(WorkerOperationsViewModel viewModel) { InitializeComponent(); DataContext = viewModel; }
    public ClientRoute Route => ClientRoute.Workers;
}
