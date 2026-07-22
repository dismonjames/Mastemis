using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;
namespace Mastemis.Client.Pages.Common;

public sealed partial class OperationalPage : Page, IClientPage
{
    public OperationalPage(ClientRoute route, string heading, string summary) { InitializeComponent(); Route = route; Heading.Text = heading; Summary.Text = summary; }
    public ClientRoute Route { get; }
}
