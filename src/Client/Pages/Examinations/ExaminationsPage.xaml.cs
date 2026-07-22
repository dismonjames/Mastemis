using Mastemis.Client.Core.Features.Examinations;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;
namespace Mastemis.Client.Pages.Examinations;

public sealed partial class ExaminationsPage : Page, IClientPage { public ExaminationsPage(ExaminationViewModel vm) { InitializeComponent(); DataContext = vm; } public ClientRoute Route => ClientRoute.Examinations; }
