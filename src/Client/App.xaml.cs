using Mastemis.Client.Core.Features.Connection;
using Mastemis.Client.Core.Networking.Http;
using Mastemis.Client.Core.Session;

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
        services.AddSingleton<IServerProbe, ServerProbe>();
        services.AddSingleton<ConnectionViewModel>();
        services.AddSingleton<MainPage>();
        services.AddHttpClient("Mastemis.Probe", client => client.Timeout = TimeSpan.FromSeconds(10));

        var provider = services.BuildServiceProvider();
        window = new Window { Content = provider.GetRequiredService<MainPage>() };
        window.Title = "Mastemis";
        window.Activate();
    }
}
