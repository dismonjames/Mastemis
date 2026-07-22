using Mastemis.Client.Core.Features.Candidates;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;

namespace Mastemis.Client.Pages.Candidates;

public sealed partial class CandidatesPage : Page, IClientPage
{
    public CandidatesPage(CandidateOperationsViewModel viewModel) { InitializeComponent(); DataContext = viewModel; }
    public ClientRoute Route => ClientRoute.Candidates;
}
