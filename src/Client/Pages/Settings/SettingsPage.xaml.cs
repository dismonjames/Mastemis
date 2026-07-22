using Mastemis.Client.Core.Features.Settings; using Mastemis.Client.Core.Navigation; using Mastemis.Client.Navigation;
namespace Mastemis.Client.Pages.Settings;
public sealed partial class SettingsPage : Page, IClientPage { public SettingsPage(SettingsViewModel vm) { InitializeComponent(); DataContext = vm; } public ClientRoute Route => ClientRoute.Settings; }
