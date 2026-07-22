using Mastemis.Client.Core.Features.Connection;

namespace Mastemis.Client;

public sealed partial class MainPage : Page
{
    public MainPage(ConnectionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
