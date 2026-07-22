using Mastemis.Client.Core.Features.Evidence;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;

namespace Mastemis.Client.Pages.Evidence;

public sealed partial class EvidencePage : Page, IClientPage
{
    public EvidencePage(EvidenceViewModel viewModel) { InitializeComponent(); DataContext = viewModel; Loaded += (_, _) => viewModel.RefreshCommand.Execute(null); }
    public ClientRoute Route => ClientRoute.Evidence;
}
