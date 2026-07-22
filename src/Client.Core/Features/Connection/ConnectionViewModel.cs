using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;
using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Core.Features.Connection;

public sealed class ConnectionViewModel : ObservableObject
{
    private readonly IServerProbe probe;
    private readonly ClientSession session;
    private string serverUrl = "https://localhost:5001";
    private ClientMode selectedMode = ClientMode.Connect;
    private bool isBusy;
    private string? statusTitle;
    private string? statusMessage;

    public ConnectionViewModel(IServerProbe probe, ClientSession session)
    {
        this.probe = probe;
        this.session = session;
        TestConnectionCommand = new AsyncCommand(TestConnectionAsync);
    }

    public IReadOnlyList<ClientMode> Modes { get; } = [ClientMode.Host, ClientMode.Connect];
    public ICommand TestConnectionCommand { get; }
    public string ServerUrl { get => serverUrl; set => SetProperty(ref serverUrl, value); }
    public ClientMode SelectedMode { get => selectedMode; set => SetProperty(ref selectedMode, value); }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string? StatusTitle { get => statusTitle; private set { if (SetProperty(ref statusTitle, value)) OnPropertyChanged(nameof(HasStatus)); } }
    public string? StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public bool HasStatus => StatusTitle is not null;

    private async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        StatusTitle = null;
        if (!TryNormalizeUri(ServerUrl, out var uri))
        {
            StatusTitle = "Invalid server URL";
            StatusMessage = "Use an HTTPS URL. HTTP is accepted only for loopback development servers.";
            return;
        }
        IsBusy = true;
        try
        {
            var result = await probe.ProbeAsync(uri, cancellationToken).ConfigureAwait(true);
            if (!result.IsAvailable)
            {
                StatusTitle = "Connection failed";
                StatusMessage = result.Error;
                return;
            }
            session.SelectServer(uri, SelectedMode);
            StatusTitle = "Connected";
            StatusMessage = result.IsReady ? $"Server is ready{VersionSuffix(result.Version)}." : "Server is live but not ready.";
        }
        finally { IsBusy = false; }
    }

    public static bool TryNormalizeUri(string value, out Uri uri)
    {
        if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var candidate) &&
            (candidate.Scheme == Uri.UriSchemeHttps || candidate.Scheme == Uri.UriSchemeHttp && candidate.IsLoopback) &&
            string.IsNullOrEmpty(candidate.UserInfo))
        {
            uri = new Uri(candidate.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/", UriKind.Absolute);
            return true;
        }
        uri = null!;
        return false;
    }

    private static string VersionSuffix(string? version) => string.IsNullOrWhiteSpace(version) ? string.Empty : $" (version {version})";
}
