using Mastemis.Client.Core.Authentication;
using Mastemis.Client.Core.Features.CandidateExam;
using Mastemis.Client.Core.Features.Candidates;
using Mastemis.Client.Core.Features.Connection;
using Mastemis.Client.Core.Features.Dashboard;
using Mastemis.Client.Core.Features.Examinations;
using Mastemis.Client.Core.Features.Health;
using Mastemis.Client.Core.Features.Login;
using Mastemis.Client.Core.Features.ProblemStudio;
using Mastemis.Client.Core.Features.Rooms;
using Mastemis.Client.Core.Features.Settings;
using Mastemis.Client.Core.Features.Shell;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Core.Networking.Http;
using Mastemis.Client.Core.Networking.Realtime;
using Mastemis.Client.Core.Session;
using Mastemis.Client.Core.Storage;
using Mastemis.Client.Navigation;
using Mastemis.Client.Pages.CandidateExam;
using Mastemis.Client.Pages.Candidates;
using Mastemis.Client.Pages.Common;
using Mastemis.Client.Pages.Connection;
using Mastemis.Client.Pages.Dashboard;
using Mastemis.Client.Pages.Errors;
using Mastemis.Client.Pages.Examinations;
using Mastemis.Client.Pages.Health;
using Mastemis.Client.Pages.Login;
using Mastemis.Client.Pages.ProblemStudio;
using Mastemis.Client.Pages.Rooms;
using Mastemis.Client.Pages.Settings;
using Mastemis.Client.Shell;
using Mastemis.Client.Storage;

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
        services.AddSingleton<IProblemPackageClient, ProblemPackageClient>();
        services.AddSingleton<ICandidateSessionClient, CandidateSessionClient>();
        services.AddSingleton<IRoomClient, RoomClient>();
        services.AddSingleton<ICandidateClient, CandidateClient>();
        services.AddSingleton<IClientPreferenceStore, UnoClientPreferenceStore>();
        services.AddSingleton<ConnectionViewModel>();
        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<ExaminationViewModel>();
        services.AddSingleton<CandidateWorkspaceViewModel>();
        services.AddSingleton<RoomOperationsViewModel>();
        services.AddSingleton<CandidateOperationsViewModel>();
        services.AddSingleton<ProblemStudioViewModel>();
        services.AddSingleton<HealthViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<ConnectionPage>();
        services.AddSingleton<LoginPage>();
        services.AddSingleton<DashboardPage>();
        services.AddSingleton<ExaminationsPage>();
        services.AddSingleton<CandidateExamPage>();
        services.AddSingleton<RoomsPage>();
        services.AddSingleton<CandidatesPage>();
        services.AddSingleton<ProblemStudioPage>();
        services.AddSingleton<HealthPage>();
        services.AddSingleton<SettingsPage>();
        services.AddSingleton<NotFoundPage>();
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<ConnectionPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<LoginPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<DashboardPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<ExaminationsPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<CandidateExamPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<RoomsPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<CandidatesPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<ProblemStudioPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<HealthPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<SettingsPage>());
        services.AddSingleton<IClientPage>(_ => new OperationalPage(ClientRoute.Submissions, "Submissions", "Submission history and server-authoritative judgement updates appear here."));
        services.AddSingleton<IClientPage>(_ => new OperationalPage(ClientRoute.Invigilation, "Invigilation", "Raw activity, evaluations, confirmed warnings, connection state, and third-warning termination are kept visually distinct."));
        services.AddSingleton<IClientPage>(_ => new OperationalPage(ClientRoute.Evidence, "Evidence metadata", "Only explicitly granted evidence metadata and access audits are shown. Binary evidence viewing is not implemented."));
        services.AddSingleton<IClientPage>(_ => new OperationalPage(ClientRoute.Problems, "Problem library", "Authorized drafts, published problems, tags, difficulty, assignments, and package workflows are accessible through Problem Studio."));
        services.AddSingleton<IClientPage>(_ => new OperationalPage(ClientRoute.Workers, "Judge workers", "Worker readiness, sandbox backend, toolchains, shared capacity, heartbeat, and failure summaries are displayed without secrets."));
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
