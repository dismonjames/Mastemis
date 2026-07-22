using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Realtime;

namespace Mastemis.Client.Core.Features.Invigilation;

public sealed class InvigilationViewModel : ObservableObject
{
    private readonly RealtimeClient realtime;
    private string examinationId = string.Empty;
    private string filter = "All";
    private string state = "Not subscribed";
    private string? error;

    public InvigilationViewModel(RealtimeClient realtime)
    {
        this.realtime = realtime;
        SubscribeCommand = new AsyncCommand(SubscribeAsync);
        realtime.EventReceived += OnEventReceived;
        realtime.StateChanged += (_, _) => { State = realtime.State.ToString(); };
    }

    public ObservableCollection<InvigilationEventItem> Events { get; } = [];
    public ObservableCollection<InvigilationEventItem> VisibleEvents { get; } = [];
    public IReadOnlyList<string> Filters { get; } = ["All", "Warning", "Session", "Submission", "Other"];
    public ICommand SubscribeCommand { get; }
    public string ExaminationId { get => examinationId; set => SetProperty(ref examinationId, value); }
    public string Filter { get => filter; set { if (SetProperty(ref filter, value)) ApplyFilter(); } }
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

    private static string Classify(string type) => type.Contains("Warning", StringComparison.OrdinalIgnoreCase) ? "Warning"
        : type.Contains("Session", StringComparison.OrdinalIgnoreCase) ? "Session"
        : type.Contains("Submission", StringComparison.OrdinalIgnoreCase) || type.Contains("Judgement", StringComparison.OrdinalIgnoreCase) ? "Submission" : "Other";
}

public sealed record InvigilationEventItem(string MessageId, string EventType, string Category, DateTimeOffset OccurredAtUtc);
