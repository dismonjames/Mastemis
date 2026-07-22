using Mastemis.Client.Core.Features.Connection;
using Mastemis.Client.Core.Authentication;
using Mastemis.Client.Core.Features.Dashboard;
using Mastemis.Client.Core.Features.Examinations;
using Mastemis.Client.Core.Features.Login;
using Mastemis.Client.Core.Features.ProblemStudio;
using Mastemis.Client.Core.Features.Shell;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Core.Networking.Http;
using Mastemis.Client.Core.Networking.Realtime;
using Mastemis.Client.Core.Session;
using Mastemis.Client.Navigation;
using Mastemis.Client.Pages.Connection;
using Mastemis.Client.Pages.Dashboard;
using Mastemis.Client.Pages.Errors;
using Mastemis.Client.Pages.Login;
using Mastemis.Client.Shell;

namespace Mastemis.Client;

public sealed partial class App : Application
{
    private Window? window;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<ClientSession>();
        services.AddSingleton<IClientNavigator, ClientNavigator>();
        services.AddSingleton<NavigationCatalog>();
        services.AddSingleton<IUiDispatcher, ImmediateUiDispatcher>();
        services.AddSingleton<RealtimeClient>();
        services.AddSingleton<IServerProbe, ServerProbe>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IApiTransport, ApiTransport>();
        services.AddSingleton<IAuthenticationClient, AuthenticationClient>();
        services.AddSingleton<IExaminationClient, ExaminationClient>();
        services.AddSingleton<IProblemDraftClient, ProblemDraftClient>();
        services.AddSingleton<IProblemMasClient, ProblemMasClient>();
        services.AddSingleton<IProblemGenerationClient, ProblemGenerationClient>();
        services.AddSingleton<ConnectionViewModel>();
        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<ConnectionPage>();
        services.AddSingleton<LoginPage>();
        services.AddSingleton<DashboardPage>();
        services.AddSingleton<NotFoundPage>();
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<ConnectionPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<LoginPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<DashboardPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<NotFoundPage>());
        services.AddSingleton<ClientPageRegistry>();
        services.AddSingleton<ShellPage>();
        services.AddHttpClient("Mastemis.Probe", client => client.Timeout = TimeSpan.FromSeconds(10));

        var provider = services.BuildServiceProvider();
        window = new Window { Content = provider.GetRequiredService<ShellPage>() };
        window.Title = "Mastemis";
        window.Activate();
    }
}
