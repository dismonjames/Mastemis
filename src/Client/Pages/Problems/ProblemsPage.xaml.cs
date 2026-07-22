using Mastemis.Client.Core.Features.Problems;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;

namespace Mastemis.Client.Pages.Problems;

public sealed partial class ProblemsPage : Page, IClientPage
{
    public ProblemsPage(ProblemLibraryViewModel viewModel) { InitializeComponent(); DataContext = viewModel; Loaded += (_, _) => viewModel.RefreshCommand.Execute(null); }
    public ClientRoute Route => ClientRoute.Problems;
}
