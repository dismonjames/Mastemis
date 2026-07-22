using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;
using Mastemis.Client.Core.Networking.Realtime;
using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Core.Features.Dashboard;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly ClientSession session;
    private readonly IServerProbe probe;
    private bool isBusy;
    private string healthState = "Not checked";
    private string? error;

    public DashboardViewModel(ClientSession session, IServerProbe probe, RealtimeClient realtime)
    {
        this.session = session;
        this.probe = probe;
        RefreshCommand = new AsyncCommand(RefreshAsync);
        session.Changed += (_, _) => RefreshIdentity();
        realtime.EventReceived += (_, envelope) =>
        {
            RecentActivity.Insert(0, new(envelope.MessageType, envelope.OccurredAtUtc, envelope.MessageId));
            while (RecentActivity.Count > 8) RecentActivity.RemoveAt(RecentActivity.Count - 1);
            OnPropertyChanged(nameof(HasRecentActivity));
        };
    }

    public ICommand RefreshCommand { get; }
    public ObservableCollection<DashboardActivity> RecentActivity { get; } = [];
    public string Greeting => $"Welcome, {session.User?.DisplayName ?? session.User?.Username ?? "user"}";
    public string RoleSummary => session.Roles.Count == 0 ? "No assigned roles" : string.Join(" · ", session.Roles.Order());
    public string Server => session.ServerBaseUri?.ToString() ?? "No server selected";
    public string HealthState { get => healthState; private set => SetProperty(ref healthState, value); }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string? Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error is not null;
    public bool HasRecentActivity => RecentActivity.Count > 0;
    public bool IsCandidate => session.HasAnyRole("Candidate");
    public bool IsInvigilator => session.HasAnyRole("ChiefInvigilator", "RoomInvigilator");
    public string DashboardContext => IsCandidate ? "Candidate overview" : IsInvigilator ? "Live examination overview" : "Operations overview";
    public string PrimaryMetricLabel => IsCandidate ? "Current examination" : IsInvigilator ? "Connected candidates" : "Active examinations";
    public string SecondaryMetricLabel => IsCandidate ? "Pending judgements" : IsInvigilator ? "Confirmed warnings" : "Judge queue";

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (session.ServerBaseUri is null) return;
        IsBusy = true;
        Error = null;
        try
        {
            var result = await probe.ProbeAsync(session.ServerBaseUri, cancellationToken).ConfigureAwait(true);
            HealthState = result.IsReady ? "Ready" : result.IsAvailable ? "Degraded" : "Unavailable";
            if (!result.IsAvailable) Error = result.Error;
        }
        finally { IsBusy = false; }
    }

    private void RefreshIdentity()
    {
        OnPropertyChanged(nameof(Greeting));
        OnPropertyChanged(nameof(RoleSummary));
        OnPropertyChanged(nameof(Server));
        OnPropertyChanged(nameof(IsCandidate));
        OnPropertyChanged(nameof(IsInvigilator));
        OnPropertyChanged(nameof(DashboardContext));
        OnPropertyChanged(nameof(PrimaryMetricLabel));
        OnPropertyChanged(nameof(SecondaryMetricLabel));
    }
}

public sealed record DashboardActivity(string Type, DateTimeOffset OccurredAtUtc, string MessageId);
