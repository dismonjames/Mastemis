using Mastemis.Client.Core.Features.CandidateExam;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;
namespace Mastemis.Client.Pages.CandidateExam;

public sealed partial class CandidateExamPage : Page, IClientPage
{
    private readonly CandidateWorkspaceViewModel viewModel;
    public CandidateExamPage(CandidateWorkspaceViewModel viewModel) { InitializeComponent(); DataContext = this.viewModel = viewModel; }
    public ClientRoute Route => ClientRoute.CandidateExam;
    private void Page_Loaded(object sender, RoutedEventArgs e) => viewModel.StartAutosave(TimeSpan.FromSeconds(5));
    private void Page_Unloaded(object sender, RoutedEventArgs e) => viewModel.StopAutosave();
}
