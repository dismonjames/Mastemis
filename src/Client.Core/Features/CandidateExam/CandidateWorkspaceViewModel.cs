using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.CandidateExam;

public sealed class CandidateWorkspaceViewModel : ObservableObject
{
    private const int MaximumSourceCharacters = 1_000_000;
    private readonly ICandidateSessionClient client;
    private readonly SemaphoreSlim saveGate = new(1, 1);
    private CancellationTokenSource? autosaveCancellation;
    private string sessionId = string.Empty;
    private string problemId = string.Empty;
    private string source = string.Empty;
    private string language = "cpp23";
    private CandidateSession? session;
    private DraftRevision? revision;
    private string saveState = "Not saved";
    private string? error;
    private bool isDirty;
    private bool isBusy;

    public CandidateWorkspaceViewModel(ICandidateSessionClient client)
    {
        this.client = client;
        LoadCommand = new AsyncCommand(LoadAsync);
        SaveCommand = new AsyncCommand(SaveAsync);
        SubmitCommand = new AsyncCommand(SubmitAsync);
        RefreshSubmissionsCommand = new AsyncCommand(RefreshSubmissionsAsync);
    }

    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SubmitCommand { get; }
    public ICommand RefreshSubmissionsCommand { get; }
    public ObservableCollection<SubmissionItem> Submissions { get; } = [];
    public IReadOnlyList<string> Languages { get; } = ["cpp23", "csharp"];
    public string SessionId { get => sessionId; set => SetProperty(ref sessionId, value); }
    public string ProblemId { get => problemId; set => SetProperty(ref problemId, value); }
    public string Source
    {
        get => source;
        set
        {
            if (value.Length > MaximumSourceCharacters || !SetProperty(ref source, value)) return;
            IsDirty = true;
            SaveState = "Unsaved changes";
        }
    }
    public string Language { get => language; set => SetProperty(ref language, value); }
    public CandidateSession? Session { get => session; private set { if (SetProperty(ref session, value)) { OnPropertyChanged(nameof(IsLocked)); OnPropertyChanged(nameof(SessionState)); OnPropertyChanged(nameof(WarningSummary)); } } }
    public string SessionState => Session?.State ?? "Not loaded";
    public string WarningSummary => Session is null ? "No session" : $"{Session.WarningCount} confirmed warning{(Session.WarningCount == 1 ? string.Empty : "s")}";
    public bool IsLocked => Session?.State is "Terminated" or "Completed";
    public bool IsDirty { get => isDirty; private set => SetProperty(ref isDirty, value); }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public bool HasSubmissions => Submissions.Count > 0;
    public string SaveState { get => saveState; private set => SetProperty(ref saveState, value); }
    public string? Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error is not null;

    public void StartAutosave(TimeSpan interval)
    {
        StopAutosave();
        autosaveCancellation = new();
        _ = AutosaveLoopAsync(interval, autosaveCancellation.Token);
    }

    public void StopAutosave()
    {
        autosaveCancellation?.Cancel();
        autosaveCancellation?.Dispose();
        autosaveCancellation = null;
    }

    private async Task LoadAsync(CancellationToken cancellationToken) => await RunAsync(async () =>
    {
        if (!Guid.TryParse(SessionId, out var id)) { Error = "Enter a valid session identifier."; return; }
        Session = await client.GetAsync(id, cancellationToken).ConfigureAwait(true);
        await RefreshSubmissionsCoreAsync(cancellationToken).ConfigureAwait(true);
    }).ConfigureAwait(true);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (Session is null || IsLocked || !IsDirty) return;
        await saveGate.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            if (!IsDirty) return;
            await RunAsync(async () =>
            {
                SaveState = "Saving…";
                revision = await client.SaveDraftAsync(Session.Id, Source, cancellationToken).ConfigureAwait(true);
                IsDirty = false;
                SaveState = $"Saved {revision.CreatedAtUtc.LocalDateTime:t}";
            }).ConfigureAwait(true);
        }
        finally { saveGate.Release(); }
    }

    private async Task SubmitAsync(CancellationToken cancellationToken) => await RunAsync(async () =>
    {
        if (Session is null || revision is null || !Guid.TryParse(ProblemId, out var id) || IsLocked) { Error = "Save a draft and provide a valid problem identifier before submitting."; return; }
        await client.SubmitAsync(Session.Id, id, revision.Id, Language, cancellationToken).ConfigureAwait(true);
        SaveState = "Submitted";
        await RefreshSubmissionsCoreAsync(cancellationToken).ConfigureAwait(true);
    }).ConfigureAwait(true);

    private async Task RefreshSubmissionsAsync(CancellationToken cancellationToken) => await RunAsync(() => RefreshSubmissionsCoreAsync(cancellationToken)).ConfigureAwait(true);
    private async Task RefreshSubmissionsCoreAsync(CancellationToken cancellationToken)
    {
        if (Session is null) return;
        var values = await client.ListSubmissionsAsync(Session.Id, cancellationToken).ConfigureAwait(true);
        Submissions.Clear();
        foreach (var value in values.OrderByDescending(value => value.Id)) Submissions.Add(value);
        OnPropertyChanged(nameof(HasSubmissions));
    }

    private async Task AutosaveLoopAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                if (IsDirty && Session is not null && !IsLocked) await SaveAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task RunAsync(Func<Task> action)
    {
        Error = null;
        IsBusy = true;
        try { await action().ConfigureAwait(true); }
        catch (ApiException value) { Error = value.Problem.Title; if (value.Problem.Code?.Contains("terminal", StringComparison.OrdinalIgnoreCase) == true && Session is not null) Session = Session with { State = "Terminated" }; }
        finally { IsBusy = false; }
    }
}
