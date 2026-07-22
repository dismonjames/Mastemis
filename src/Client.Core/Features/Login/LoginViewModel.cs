using System.Windows.Input;
using Mastemis.Client.Core.Authentication;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Core.Networking.Http;
using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Core.Features.Login;

public sealed class LoginViewModel : ObservableObject
{
    private readonly IAuthenticationClient authentication;
    private readonly IClientNavigator navigator;
    private readonly ClientSession session;
    private string username = string.Empty;
    private string password = string.Empty;
    private bool rememberMe;
    private bool isBusy;
    private string? errorMessage;

    public LoginViewModel(IAuthenticationClient authentication, IClientNavigator navigator, ClientSession session)
    {
        this.authentication = authentication;
        this.navigator = navigator;
        this.session = session;
        LoginCommand = new AsyncCommand(LoginAsync);
        ChangeServerCommand = new AsyncCommand(ChangeServerAsync);
    }

    public ICommand LoginCommand { get; }
    public ICommand ChangeServerCommand { get; }
    public string Username { get => username; set => SetProperty(ref username, value); }
    public string Password { get => password; set => SetProperty(ref password, value); }
    public bool RememberMe { get => rememberMe; set => SetProperty(ref rememberMe, value); }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string? ErrorMessage { get => errorMessage; private set { if (SetProperty(ref errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => ErrorMessage is not null;
    public string ServerName => session.ServerBaseUri?.Host ?? "No server selected";
    public string ServerAddress => session.ServerBaseUri?.GetLeftPart(UriPartial.Authority) ?? string.Empty;
    public string ModeLabel => session.Mode == ClientMode.Host ? "Host Mode" : "Connect Mode";

    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        IsBusy = true; ErrorMessage = null;
        try
        {
            if (string.IsNullOrWhiteSpace(Username) || Password.Length == 0)
            {
                ErrorMessage = "Username and password are required.";
                return;
            }
            await authentication.LoginAsync(Username.Trim(), Password, RememberMe, cancellationToken).ConfigureAwait(true);
            Password = string.Empty;
            navigator.Navigate(ClientRoute.Dashboard);
        }
        catch (ApiException error)
        {
            ErrorMessage = error.Problem.Code switch
            {
                "identity.disabled" => "This account is disabled. Contact an administrator.",
                "identity.password_change_required" => "A password change is required before this account can continue.",
                _ => error.Problem.Title
            };
        }
        finally { IsBusy = false; }
    }

    private Task ChangeServerAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        Password = string.Empty;
        navigator.Navigate(ClientRoute.Connection);
        return Task.CompletedTask;
    }
}
