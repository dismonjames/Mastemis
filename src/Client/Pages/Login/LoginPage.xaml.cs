using Mastemis.Client.Core.Features.Login;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;

namespace Mastemis.Client.Pages.Login;

public sealed partial class LoginPage : Page, IClientPage
{
    private readonly LoginViewModel viewModel;
    public LoginPage(LoginViewModel viewModel) { InitializeComponent(); DataContext = this.viewModel = viewModel; }
    public ClientRoute Route => ClientRoute.Login;
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) => viewModel.Password = ((PasswordBox)sender).Password;
}
