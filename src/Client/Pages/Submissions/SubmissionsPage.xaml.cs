using Mastemis.Client.Core.Features.CandidateExam;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;

namespace Mastemis.Client.Pages.Submissions;

public sealed partial class SubmissionsPage : Page, IClientPage
{
    public SubmissionsPage(CandidateWorkspaceViewModel viewModel) { InitializeComponent(); DataContext = viewModel; }
    public ClientRoute Route => ClientRoute.Submissions;
}
