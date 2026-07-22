using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Core.Features.Dashboard;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly ClientSession session;
    public DashboardViewModel(ClientSession session) { this.session = session; session.Changed += (_, _) => Refresh(); }
    public string Greeting => $"Welcome, {session.User?.DisplayName ?? session.User?.Username ?? "user"}";
    public string RoleSummary => session.Roles.Count == 0 ? "No assigned roles" : string.Join(" · ", session.Roles.Order());
    public string Server => session.ServerBaseUri?.ToString() ?? "No server selected";
    private void Refresh() { OnPropertyChanged(nameof(Greeting)); OnPropertyChanged(nameof(RoleSummary)); OnPropertyChanged(nameof(Server)); }
}
