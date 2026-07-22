using System.Windows.Input;
using Mastemis.Client.Core.Authentication;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Core.Networking.Realtime;
using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Core.Features.Shell;

public sealed class ShellViewModel : ObservableObject
{
    private readonly ClientSession session;
    private readonly NavigationCatalog catalog;
    private readonly IClientNavigator navigator;
    private readonly IAuthenticationClient authentication;
    private readonly RealtimeClient realtime;

    public ShellViewModel(ClientSession session, NavigationCatalog catalog, IClientNavigator navigator, IAuthenticationClient authentication, RealtimeClient realtime)
    {
        this.session = session; this.catalog = catalog; this.navigator = navigator; this.authentication = authentication; this.realtime = realtime;
        LogoutCommand = new AsyncCommand(LogoutAsync);
        session.Changed += (_, _) => { OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(IsAuthenticated)); OnPropertyChanged(nameof(NavigationItems)); };
        realtime.StateChanged += (_, _) => OnPropertyChanged(nameof(RealtimeState));
    }

    public ICommand LogoutCommand { get; }
    public bool IsAuthenticated => session.IsAuthenticated;
    public string DisplayName => session.User?.DisplayName ?? session.User?.Username ?? "Not signed in";
    public string ServerName => session.ServerBaseUri?.Host ?? "No server";
    public string RealtimeState => realtime.State.ToString();
    public IReadOnlyList<NavigationDescriptor> NavigationItems => catalog.For(session);

    private async Task LogoutAsync(CancellationToken cancellationToken)
    {
        await realtime.DisposeAsync().ConfigureAwait(true);
        await authentication.LogoutAsync(cancellationToken).ConfigureAwait(true);
        navigator.Navigate(ClientRoute.Login);
    }
}
