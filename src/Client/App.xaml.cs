using Mastemis.Client.Core.Authentication;
using Mastemis.Client.Core.Diagnostics;
using Mastemis.Client.Core.Features.About;
using Mastemis.Client.Core.Features.CandidateExam;
using Mastemis.Client.Core.Features.Candidates;
using Mastemis.Client.Core.Features.Connection;
using Mastemis.Client.Core.Features.Dashboard;
using Mastemis.Client.Core.Features.Evidence;
using Mastemis.Client.Core.Features.Examinations;
using Mastemis.Client.Core.Features.Health;
using Mastemis.Client.Core.Features.Invigilation;
using Mastemis.Client.Core.Features.Login;
using Mastemis.Client.Core.Features.Problems;
using Mastemis.Client.Core.Features.ProblemStudio;
using Mastemis.Client.Core.Features.ProblemStudio.Activity;
using Mastemis.Client.Core.Features.ProblemStudio.Assets;
using Mastemis.Client.Core.Features.ProblemStudio.Metadata;
using Mastemis.Client.Core.Features.ProblemStudio.Overview;
using Mastemis.Client.Core.Features.ProblemStudio.Packages;
using Mastemis.Client.Core.Features.ProblemStudio.Permissions;
using Mastemis.Client.Core.Features.ProblemStudio.ReferenceSolution;
using Mastemis.Client.Core.Features.ProblemStudio.Statements;
using Mastemis.Client.Core.Features.ProblemStudio.Tests;
using Mastemis.Client.Core.Features.Rooms;
using Mastemis.Client.Core.Features.Settings;
using Mastemis.Client.Core.Features.Shell;
using Mastemis.Client.Core.Features.Workers;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Core.Networking.Http;
using Mastemis.Client.Core.Networking.Realtime;
using Mastemis.Client.Core.Platform.Files;
using Mastemis.Client.Core.Session;
using Mastemis.Client.Core.Storage;
using Mastemis.Client.Navigation;
using Mastemis.Client.Pages.About;
using Mastemis.Client.Pages.CandidateExam;
using Mastemis.Client.Pages.Candidates;
using Mastemis.Client.Pages.Connection;
using Mastemis.Client.Pages.Dashboard;
using Mastemis.Client.Pages.Errors;
using Mastemis.Client.Pages.Evidence;
using Mastemis.Client.Pages.Examinations;
using Mastemis.Client.Pages.Health;
using Mastemis.Client.Pages.Invigilation;
using Mastemis.Client.Pages.Login;
using Mastemis.Client.Pages.Problems;
using Mastemis.Client.Pages.ProblemStudio;
using Mastemis.Client.Pages.Rooms;
using Mastemis.Client.Pages.Settings;
using Mastemis.Client.Pages.Submissions;
using Mastemis.Client.Pages.Workers;
using Mastemis.Client.Platform.Files;
using Mastemis.Client.Shell;
using Mastemis.Client.Storage;

namespace Mastemis.Client;

public sealed partial class App : Application
{
    private Window? window;
    private readonly string[] arguments;

    public App() : this([]) { }
    public App(string[] arguments)
    {
        this.arguments = arguments;
        UnhandledException += (_, eventArgs) =>
        {
            StartupDiagnosticWriter.CreateDefault().WriteFatal(eventArgs.Exception);
            Environment.ExitCode = 1;
        };
        InitializeComponent();
    }

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
        services.AddSingleton<IDashboardClient, DashboardClient>();
        services.AddSingleton<IWorkerInventoryClient, WorkerInventoryClient>();
        services.AddSingleton<IInvigilationClient, InvigilationClient>();
        services.AddSingleton<IProblemLibraryClient, ProblemLibraryClient>();
        services.AddSingleton<IProblemDraftClient, ProblemDraftClient>();
        services.AddSingleton<IProblemMasClient, ProblemMasClient>();
        services.AddSingleton<IProblemGenerationClient, ProblemGenerationClient>();
        services.AddSingleton<IProblemPackageClient, ProblemPackageClient>();
        services.AddSingleton<IProblemStatementClient, ProblemStatementClient>();
        services.AddSingleton<IProblemAssetClient, ProblemAssetClient>();
        services.AddSingleton<IReferenceSolutionClient, ReferenceSolutionClient>();
        services.AddSingleton<IProblemTestClient, ProblemTestClient>();
        services.AddSingleton<IProblemPermissionClient, ProblemPermissionClient>();
        services.AddSingleton<IProblemActivityClient, ProblemActivityClient>();
        services.AddSingleton<IProblemOverviewClient, ProblemOverviewClient>();
        services.AddSingleton<IClientFileService, UnoClientFileService>();
        services.AddSingleton<ICandidateSessionClient, CandidateSessionClient>();
        services.AddSingleton<IRoomClient, RoomClient>();
        services.AddSingleton<ICandidateClient, CandidateClient>();
        services.AddSingleton<IEvidenceClient, EvidenceClient>();
        services.AddSingleton<IClientPreferenceStore, UnoClientPreferenceStore>();
        services.AddSingleton<ConnectionViewModel>();
        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<ExaminationViewModel>();
        services.AddSingleton<CandidateWorkspaceViewModel>();
        services.AddSingleton<RoomOperationsViewModel>();
        services.AddSingleton<CandidateOperationsViewModel>();
        services.AddSingleton<EvidenceViewModel>();
        services.AddSingleton<InvigilationViewModel>();
        services.AddSingleton<ProblemLibraryViewModel>();
        services.AddSingleton<ProblemStudioViewModel>();
        services.AddSingleton<ProblemMetadataViewModel>();
        services.AddSingleton<StatementAuthoringViewModel>();
        services.AddSingleton<ProblemAssetViewModel>();
        services.AddSingleton<ReferenceSolutionViewModel>();
        services.AddSingleton<ProblemTestViewModel>();
        services.AddSingleton<ProblemPackageViewModel>();
        services.AddSingleton<ProblemPermissionViewModel>();
        services.AddSingleton<ProblemActivityViewModel>();
        services.AddSingleton<ProblemOverviewViewModel>();
        services.AddSingleton<HealthViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<WorkerOperationsViewModel>();
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
        services.AddSingleton<SubmissionsPage>();
        services.AddSingleton<EvidencePage>();
        services.AddSingleton<InvigilationPage>();
        services.AddSingleton<ProblemsPage>();
        services.AddSingleton<WorkersPage>();
        services.AddSingleton<NotFoundPage>();
        services.AddSingleton<AboutPage>();
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
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<SubmissionsPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<InvigilationPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<EvidencePage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<ProblemsPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<WorkersPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<AboutPage>());
        services.AddSingleton<IClientPage>(provider => provider.GetRequiredService<NotFoundPage>());
        services.AddSingleton<ClientPageRegistry>();
        services.AddSingleton<ShellPage>();
        services.AddHttpClient("Mastemis.Probe", client => client.Timeout = TimeSpan.FromSeconds(10));

        var provider = services.BuildServiceProvider();
        var review = VisualReviewOptions.Parse(arguments,
            string.Equals(Environment.GetEnvironmentVariable("MASTEMIS_ENABLE_VISUAL_REVIEW"), "1", StringComparison.Ordinal));
        if (review is not null)
        {
            var session = provider.GetRequiredService<ClientSession>();
            session.SelectServer(new Uri("https://visual-review.invalid"), ClientMode.Connect);
            session.Authenticate(new(Guid.Empty, "visual-review", "Visual Review", [review.Role]));
            provider.GetRequiredService<IClientNavigator>().Navigate(review.Route);
        }
        var shell = provider.GetRequiredService<ShellPage>();
        if (review is not null) shell.RequestedTheme = string.Equals(review.Theme, "light", StringComparison.OrdinalIgnoreCase)
            ? ElementTheme.Light : ElementTheme.Dark;
        window = new Window { Content = shell };
        window.Title = "Mastemis";
        window.Activate();
        if (review is not null) window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = review.Width, Height = review.Height });
    }
}
