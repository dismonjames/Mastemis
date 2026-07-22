using Mastemis.Client.Core.Features.Health;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;
namespace Mastemis.Client.Pages.Health;

public sealed partial class HealthPage : Page, IClientPage { public HealthPage(HealthViewModel vm) { InitializeComponent(); DataContext = vm; } public ClientRoute Route => ClientRoute.Health; }
