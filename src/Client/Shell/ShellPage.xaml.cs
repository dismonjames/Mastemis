using Mastemis.Client.Core.Diagnostics;
using Mastemis.Client.Core.Features.Settings;
using Mastemis.Client.Core.Features.Shell;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Core.VisualReview;
using Mastemis.Client.Navigation;
using Mastemis.Client.VisualReview;

namespace Mastemis.Client.Shell;

public sealed partial class ShellPage : Page
{
    private readonly ShellViewModel viewModel;
    private readonly IClientNavigator navigator;
    private readonly ClientPageRegistry pages;
    private VisualFixture? fixture;

    public ShellPage(ShellViewModel viewModel, SettingsViewModel settings, IClientNavigator navigator, ClientPageRegistry pages)
    {
        InitializeComponent();
        DataContext = viewModel;
        this.viewModel = viewModel;
        this.navigator = navigator;
        this.pages = pages;
        settings.ThemeChanged += (_, theme) => RequestedTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        navigator.RouteChanged += (_, route) => Show(route);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ShellViewModel.NavigationItems)) BuildNavigation();
            if (args.PropertyName == nameof(ShellViewModel.IsAuthenticated)) Show(navigator.Current);
        };
        BuildNavigation();
        Show(navigator.Current);
    }

    public void EnableVisualReview(VisualFixture value, VisualReviewOptions options)
    {
        fixture = value;
        var panel = new VisualFixturePanel { DataContext = value };
        panel.SetReviewContext(options);
        OnboardingReviewFixture.Content = panel;
        AuthenticatedReviewFixture.Content = new VisualFixturePanel { DataContext = value };
        ((VisualFixturePanel)AuthenticatedReviewFixture.Content).SetReviewContext(options);
        Show(navigator.Current);
    }

    private void BuildNavigation()
    {
        Navigation.MenuItems.Clear();
        string? group = null;
        foreach (var item in viewModel.NavigationItems)
        {
            if (!string.Equals(group, item.Group, StringComparison.Ordinal))
            {
                group = item.Group;
                Navigation.MenuItems.Add(new NavigationViewItemHeader { Content = group });
            }
            Navigation.MenuItems.Add(new NavigationViewItem { Content = item.Label, Tag = item.Route, Icon = new FontIcon { Glyph = item.Glyph } });
        }
    }

    private void Navigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is ClientRoute route) navigator.Navigate(route);
    }

    private void Show(ClientRoute route)
    {
        var onboarding = route is ClientRoute.Connection or ClientRoute.Login || !viewModel.IsAuthenticated;
        OnboardingRoot.Visibility = onboarding ? Visibility.Visible : Visibility.Collapsed;
        Navigation.Visibility = onboarding ? Visibility.Collapsed : Visibility.Visible;
        OnboardingReviewFixture.Visibility = onboarding && fixture is not null ? Visibility.Visible : Visibility.Collapsed;
        AuthenticatedReviewFixture.Visibility = !onboarding && fixture is not null ? Visibility.Visible : Visibility.Collapsed;

        if (onboarding)
        {
            var safeRoute = route == ClientRoute.Login || route == ClientRoute.Connection
                ? route
                : ClientRoute.Connection;
            OnboardingFrame.SetValue(Grid.RowProperty, fixture is null ? 0 : 1);
            OnboardingFrame.Content = pages.Resolve(safeRoute);
            return;
        }

        if (!viewModel.NavigationItems.Any(item => item.Route == route))
        {
            route = ClientRoute.Dashboard;
        }
        ContentFrame.Content = pages.Resolve(route);
        Navigation.SelectedItem = Navigation.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => Equals(item.Tag, route));
    }
}
