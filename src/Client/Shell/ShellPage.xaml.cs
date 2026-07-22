using Mastemis.Client.Core.Features.Shell;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Navigation;

namespace Mastemis.Client.Shell;

public sealed partial class ShellPage : Page
{
    private readonly ShellViewModel viewModel;
    private readonly IClientNavigator navigator;
    private readonly ClientPageRegistry pages;

    public ShellPage(ShellViewModel viewModel, IClientNavigator navigator, ClientPageRegistry pages)
    {
        InitializeComponent(); DataContext = viewModel;
        this.viewModel = viewModel; this.navigator = navigator; this.pages = pages;
        navigator.RouteChanged += (_, route) => Show(route);
        viewModel.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(ShellViewModel.NavigationItems)) BuildNavigation(); };
        BuildNavigation(); Show(navigator.Current);
    }

    private void BuildNavigation()
    {
        Navigation.MenuItems.Clear();
        foreach (var item in viewModel.NavigationItems)
            Navigation.MenuItems.Add(new NavigationViewItem { Content = item.Label, Tag = item.Route, Icon = new FontIcon { Glyph = item.Glyph } });
    }

    private void Navigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is ClientRoute route) navigator.Navigate(route);
    }

    private void Show(ClientRoute route) => ContentFrame.Content = pages.Resolve(route);
}
