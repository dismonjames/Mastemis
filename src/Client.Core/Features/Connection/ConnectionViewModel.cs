using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Core.Networking.Http;
using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Core.Features.Connection;

public enum ConnectionPresentationState
{
    Idle,
    Validating,
    Connecting,
    Compatible,
    Degraded,
    Incompatible,
    Unavailable,
    TlsFailure
}

public sealed class ConnectionViewModel : ObservableObject
{
    private readonly IServerProbe probe;
    private readonly ClientSession session;
    private readonly IClientNavigator navigator;
    private string serverUrl = "https://localhost:5001";
    private ClientMode selectedMode = ClientMode.Connect;
    private bool isBusy;
    private string? statusTitle;
    private string? statusMessage;
    private string? serverVersion;
    private ConnectionPresentationState state;

    public ConnectionViewModel(IServerProbe probe, ClientSession session, IClientNavigator navigator)
    {
        this.probe = probe;
        this.session = session;
        this.navigator = navigator;
        TestConnectionCommand = new AsyncCommand(TestConnectionAsync);
        ContinueCommand = new AsyncCommand(ContinueAsync);
        EditServerCommand = new AsyncCommand(EditServerAsync);
    }

    public ICommand TestConnectionCommand { get; }
    public ICommand ContinueCommand { get; }
    public ICommand EditServerCommand { get; }
    public string ServerUrl
    {
        get => serverUrl;
        set
        {
            if (SetProperty(ref serverUrl, value))
            {
                OnPropertyChanged(nameof(IsDevelopmentHttp));
                if (State is ConnectionPresentationState.Compatible or ConnectionPresentationState.Degraded)
                    ResetStatus();
            }
        }
    }
    public ClientMode SelectedMode
    {
        get => selectedMode;
        set
        {
            if (!SetProperty(ref selectedMode, value)) return;
            OnPropertyChanged(nameof(IsHostSelected));
            OnPropertyChanged(nameof(IsConnectSelected));
        }
    }
    public bool IsHostSelected { get => SelectedMode == ClientMode.Host; set { if (value) SelectedMode = ClientMode.Host; } }
    public bool IsConnectSelected { get => SelectedMode == ClientMode.Connect; set { if (value) SelectedMode = ClientMode.Connect; } }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string? StatusTitle { get => statusTitle; private set { if (SetProperty(ref statusTitle, value)) OnPropertyChanged(nameof(HasStatus)); } }
    public string? StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public string? ServerVersion { get => serverVersion; private set => SetProperty(ref serverVersion, value); }
    public ConnectionPresentationState State
    {
        get => state;
        private set
        {
            if (!SetProperty(ref state, value)) return;
            OnPropertyChanged(nameof(IsSuccessful));
            OnPropertyChanged(nameof(IsFailure));
            OnPropertyChanged(nameof(CanContinue));
        }
    }
    public bool HasStatus => StatusTitle is not null;
    public bool IsSuccessful => State is ConnectionPresentationState.Compatible or ConnectionPresentationState.Degraded;
    public bool IsFailure => State is ConnectionPresentationState.Incompatible or ConnectionPresentationState.Unavailable or ConnectionPresentationState.TlsFailure;
    public bool CanContinue => State is ConnectionPresentationState.Compatible or ConnectionPresentationState.Degraded;
    public bool IsDevelopmentHttp => Uri.TryCreate(ServerUrl, UriKind.Absolute, out var value) && value.Scheme == Uri.UriSchemeHttp;

    private async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        ResetStatus();
        State = ConnectionPresentationState.Validating;
        if (!TryNormalizeUri(ServerUrl, out var uri))
        {
            State = ConnectionPresentationState.Incompatible;
            StatusTitle = "Invalid server URL";
            StatusMessage = "Use an HTTPS URL. HTTP is accepted only for loopback development servers.";
            return;
        }
        IsBusy = true;
        State = ConnectionPresentationState.Connecting;
        try
        {
            var result = await probe.ProbeAsync(uri, cancellationToken).ConfigureAwait(true);
            if (!result.IsAvailable)
            {
                State = result.ErrorKind == "tls" ? ConnectionPresentationState.TlsFailure : ConnectionPresentationState.Unavailable;
                StatusTitle = State == ConnectionPresentationState.TlsFailure ? "TLS validation failed" : "Server unavailable";
                StatusMessage = result.Error;
                return;
            }
            session.SelectServer(uri, SelectedMode);
            ServerVersion = result.Version;
            State = result.IsReady ? ConnectionPresentationState.Compatible : ConnectionPresentationState.Degraded;
            StatusTitle = result.IsReady ? "Server is ready" : "Server is live but degraded";
            StatusMessage = result.IsReady
                ? $"Mastemis API responded successfully{VersionSuffix(result.Version)}."
                : "Authentication may be unavailable until all readiness checks pass.";
        }
        finally { IsBusy = false; }
    }

    private Task ContinueAsync(CancellationToken cancellationToken)
    {
        if (CanContinue) navigator.Navigate(ClientRoute.Login);
        return Task.CompletedTask;
    }

    private Task EditServerAsync(CancellationToken cancellationToken)
    {
        ResetStatus();
        return Task.CompletedTask;
    }

    private void ResetStatus()
    {
        State = ConnectionPresentationState.Idle;
        StatusTitle = null;
        StatusMessage = null;
        ServerVersion = null;
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
