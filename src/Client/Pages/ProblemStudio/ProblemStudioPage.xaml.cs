using Mastemis.Client.Core.Features.ProblemStudio; using Mastemis.Client.Core.Navigation; using Mastemis.Client.Navigation;
namespace Mastemis.Client.Pages.ProblemStudio;
public sealed partial class ProblemStudioPage : Page, IClientPage { public ProblemStudioPage(ProblemStudioViewModel vm) { InitializeComponent(); DataContext = vm; } public ClientRoute Route => ClientRoute.ProblemStudio; }
