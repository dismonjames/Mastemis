using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.CandidateExam;

public sealed class CandidateWorkspaceViewModel : ObservableObject
{
    private const int MaximumSourceCharacters = 1_000_000;
    private readonly ICandidateSessionClient client;
    private string sessionId = string.Empty;
    private string problemId = string.Empty;
    private string source = string.Empty;
    private string language = "cpp23";
    private CandidateSession? session;
    private DraftRevision? revision;
    private string saveState = "Not saved";
    private string? error;
    public CandidateWorkspaceViewModel(ICandidateSessionClient client)
    {
        this.client = client;
        LoadCommand = new AsyncCommand(LoadAsync); SaveCommand = new AsyncCommand(SaveAsync); SubmitCommand = new AsyncCommand(SubmitAsync);
    }
    public ICommand LoadCommand { get; } public ICommand SaveCommand { get; } public ICommand SubmitCommand { get; }
    public IReadOnlyList<string> Languages { get; } = ["cpp23", "csharp"];
    public string SessionId { get => sessionId; set => SetProperty(ref sessionId, value); }
    public string ProblemId { get => problemId; set => SetProperty(ref problemId, value); }
    public string Source { get => source; set { if (value.Length <= MaximumSourceCharacters && SetProperty(ref source, value)) SaveState = "Unsaved changes"; } }
    public string Language { get => language; set => SetProperty(ref language, value); }
    public CandidateSession? Session { get => session; private set { if (SetProperty(ref session, value)) { OnPropertyChanged(nameof(IsLocked)); OnPropertyChanged(nameof(SessionState)); } } }
    public string SessionState => Session?.State ?? "Not loaded";
    public bool IsLocked => Session?.State is "Terminated" or "Completed";
    public string SaveState { get => saveState; private set => SetProperty(ref saveState, value); }
    public string? Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error is not null;
    private async Task LoadAsync(CancellationToken ct) => await RunAsync(async () => { if (Guid.TryParse(SessionId, out var id)) Session = await client.GetAsync(id, ct); else Error = "Enter a valid session identifier."; }).ConfigureAwait(true);
    private async Task SaveAsync(CancellationToken ct) => await RunAsync(async () => { if (Session is null || IsLocked) return; revision = await client.SaveDraftAsync(Session.Id, Source, ct); SaveState = $"Saved {revision.CreatedAtUtc.LocalDateTime:t}"; }).ConfigureAwait(true);
    private async Task SubmitAsync(CancellationToken ct) => await RunAsync(async () => { if (Session is null || revision is null || !Guid.TryParse(ProblemId, out var id) || IsLocked) { Error = "Save a draft and provide a valid problem identifier before submitting."; return; } await client.SubmitAsync(Session.Id, id, revision.Id, Language, ct); SaveState = "Submitted"; }).ConfigureAwait(true);
    private async Task RunAsync(Func<Task> action) { Error = null; try { await action().ConfigureAwait(true); } catch (ApiException value) { Error = value.Problem.Title; if (value.Problem.Code?.Contains("terminal", StringComparison.OrdinalIgnoreCase) == true && Session is not null) Session = Session with { State = "Terminated" }; } }
}
