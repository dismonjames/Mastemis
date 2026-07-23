using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Realtime;

namespace Mastemis.Client.Core.Features.Invigilation;

public sealed class InvigilationViewModel : ObservableObject
{
    private readonly RealtimeClient realtime;
    private readonly IInvigilationClient client;
    private string examinationId = string.Empty;
    private string filter = "All";
    private string search = string.Empty, connectionFilter = "All", warningFilter = "All", severityFilter = "All";
    private bool terminatedOnly, staleOnly;
    private LiveCandidate? selectedCandidate;
    private InvigilationSnapshot? snapshot;
    private string state = "Not subscribed";
    private string? error;

    public InvigilationViewModel(RealtimeClient realtime, IInvigilationClient client)
    {
        this.realtime = realtime;
        this.client = client;
        SubscribeCommand = new AsyncCommand(SubscribeAsync);
        ClearFiltersCommand = new AsyncCommand(_ => { ClearFilters(); return Task.CompletedTask; });
        realtime.EventReceived += OnEventReceived;
        realtime.StateChanged += (_, _) => { State = realtime.State.ToString(); };
    }

    public ObservableCollection<InvigilationEventItem> Events { get; } = [];
    public ObservableCollection<InvigilationEventItem> VisibleEvents { get; } = [];
    public ObservableCollection<LiveCandidate> Candidates { get; } = [];
    public ObservableCollection<LiveCandidate> VisibleCandidates { get; } = [];
    public ObservableCollection<LiveRoom> Rooms { get; } = [];
    public ObservableCollection<LiveWarning> CandidateWarnings { get; } = [];
    public ObservableCollection<LiveSfeEvent> CandidateEvents { get; } = [];
    public IReadOnlyList<string> Filters { get; } = ["All", "Warning", "Session", "Submission", "Other"];
    public ICommand SubscribeCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public string ExaminationId { get => examinationId; set => SetProperty(ref examinationId, value); }
    public string Filter { get => filter; set { if (SetProperty(ref filter, value)) ApplyFilter(); } }
    public string Search { get => search; set { if (SetProperty(ref search, value)) ApplyCandidateFilters(); } }
    public string ConnectionFilter { get => connectionFilter; set { if (SetProperty(ref connectionFilter, value)) ApplyCandidateFilters(); } }
    public string WarningFilter { get => warningFilter; set { if (SetProperty(ref warningFilter, value)) ApplyCandidateFilters(); } }
    public string SeverityFilter { get => severityFilter; set { if (SetProperty(ref severityFilter, value)) ApplyCandidateFilters(); } }
    public bool TerminatedOnly { get => terminatedOnly; set { if (SetProperty(ref terminatedOnly, value)) ApplyCandidateFilters(); } }
    public bool StaleOnly { get => staleOnly; set { if (SetProperty(ref staleOnly, value)) ApplyCandidateFilters(); } }
    public LiveCandidate? SelectedCandidate { get => selectedCandidate; set { if (SetProperty(ref selectedCandidate, value)) ApplyCandidateDetail(); } }
    public int VisibleCandidateCount => VisibleCandidates.Count;
    public string State { get => state; private set => SetProperty(ref state, value); }
    public string? Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error is not null;
    public bool IsEmpty => VisibleEvents.Count == 0;

    private async Task SubscribeAsync(CancellationToken cancellationToken)
    {
        Error = null;
        if (!Guid.TryParse(ExaminationId, out var id)) { Error = "Enter a valid examination identifier."; return; }
        try
        {
            await realtime.ConnectAsync(cancellationToken).ConfigureAwait(true);
            await realtime.JoinAsync("exam", id, cancellationToken).ConfigureAwait(true);
            var value = await client.GetExamAsync(id, cancellationToken).ConfigureAwait(true);
            if (value is not null) ApplySnapshot(value);
            State = "Live · examination scope joined";
        }
        catch (Exception value) when (value is InvalidOperationException or HttpRequestException)
        {
            Error = "Realtime connection could not be established.";
        }
    }

    private void OnEventReceived(object? sender, RealtimeEnvelope envelope)
    {
        var category = Classify(envelope.MessageType);
        Events.Insert(0, new(envelope.MessageId, envelope.MessageType, category, envelope.OccurredAtUtc));
        while (Events.Count > 500) Events.RemoveAt(Events.Count - 1);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        VisibleEvents.Clear();
        foreach (var value in Events.Where(x => Filter == "All" || x.Category == Filter)) VisibleEvents.Add(value);
        OnPropertyChanged(nameof(IsEmpty));
    }

    public void ClearFilters()
    {
        Search = string.Empty; ConnectionFilter = "All"; WarningFilter = "All"; SeverityFilter = "All";
        TerminatedOnly = false; StaleOnly = false; ApplyCandidateFilters();
    }

    public void ApplySnapshot(InvigilationSnapshot value)
    {
        snapshot = value; Candidates.Clear(); Rooms.Clear();
        foreach (var item in value.Candidates) Candidates.Add(item);
        foreach (var item in value.Rooms) Rooms.Add(item);
        ApplyCandidateFilters(); ApplyCandidateDetail();
    }

    private void ApplyCandidateFilters()
    {
        var now = snapshot?.GeneratedAtUtc ?? DateTimeOffset.UtcNow;
        var query = Candidates.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(Search)) query = query.Where(x => x.DisplayName.Contains(Search, StringComparison.OrdinalIgnoreCase));
        if (ConnectionFilter != "All") query = query.Where(x => x.ConnectionState == ConnectionFilter);
        if (WarningFilter == "With warnings") query = query.Where(x => x.WarningCount > 0);
        if (WarningFilter == "No warnings") query = query.Where(x => x.WarningCount == 0);
        if (TerminatedOnly) query = query.Where(x => x.Terminated);
        if (StaleOnly) query = query.Where(x => x.LatestActivityUtc is null || now - x.LatestActivityUtc > TimeSpan.FromMinutes(2));
        if (SeverityFilter != "All" && snapshot is not null)
        {
            var sessions = snapshot.RecentWarnings.Where(x => x.Severity == SeverityFilter).Select(x => x.SessionId).ToHashSet();
            query = query.Where(x => sessions.Contains(x.SessionId));
        }
        VisibleCandidates.Clear(); foreach (var item in query) VisibleCandidates.Add(item);
        OnPropertyChanged(nameof(VisibleCandidateCount));
        if (SelectedCandidate is not null && !VisibleCandidates.Any(x => x.CandidateId == SelectedCandidate.CandidateId)) SelectedCandidate = null;
    }

    private void ApplyCandidateDetail()
    {
        CandidateWarnings.Clear(); CandidateEvents.Clear();
        if (SelectedCandidate is null || snapshot is null) return;
        foreach (var item in snapshot.RecentWarnings.Where(x => x.SessionId == SelectedCandidate.SessionId).OrderByDescending(x => x.IssuedAtUtc)) CandidateWarnings.Add(item);
        foreach (var item in snapshot.RecentEvents.Where(x => x.SessionId == SelectedCandidate.SessionId).OrderByDescending(x => x.ReceivedAtUtc)) CandidateEvents.Add(item);
    }

    private static string Classify(string type) => type.Contains("Warning", StringComparison.OrdinalIgnoreCase) ? "Warning"
        : type.Contains("Session", StringComparison.OrdinalIgnoreCase) ? "Session"
        : type.Contains("Submission", StringComparison.OrdinalIgnoreCase) || type.Contains("Judgement", StringComparison.OrdinalIgnoreCase) ? "Submission" : "Other";
}

public sealed record InvigilationEventItem(string MessageId, string EventType, string Category, DateTimeOffset OccurredAtUtc);
