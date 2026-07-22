using Mastemis.Client.Core.Features.CandidateExam;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;
namespace Mastemis.Client.Pages.CandidateExam;

public sealed partial class CandidateExamPage : Page, IClientPage { public CandidateExamPage(CandidateWorkspaceViewModel vm) { InitializeComponent(); DataContext = vm; } public ClientRoute Route => ClientRoute.CandidateExam; }
