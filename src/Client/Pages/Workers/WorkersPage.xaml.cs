using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;

namespace Mastemis.Client.Pages.Workers;

public sealed partial class WorkersPage : Page, IClientPage
{
    public WorkersPage() => InitializeComponent();
    public ClientRoute Route => ClientRoute.Workers;
}
