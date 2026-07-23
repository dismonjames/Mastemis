using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio.Metadata;

public sealed class ProblemMetadataViewModel : ObservableObject
{
    private readonly IProblemDraftClient client;
    private ProblemDraftSummary? draft; private string title = string.Empty; private string authors = string.Empty;
    private string tags = string.Empty; private string difficulty = "unspecified"; private string locale = "en";
    private string languages = "cpp, csharp"; private string checker = "exact"; private long time = 1000, memory = 268435456, output = 1048576;
    private string? error; private string status = "No draft loaded";
    public ProblemMetadataViewModel(IProblemDraftClient client) { this.client = client; SaveCommand = new AsyncCommand(SaveAsync); }
    public ICommand SaveCommand { get; }
    public string Title { get => title; set { SetProperty(ref title, value); Dirty(); } }
    public string Authors { get => authors; set { SetProperty(ref authors, value); Dirty(); } }
    public string Tags { get => tags; set { SetProperty(ref tags, value); Dirty(); } }
    public string Difficulty { get => difficulty; set { SetProperty(ref difficulty, value); Dirty(); } }
    public string DefaultLocale { get => locale; set { SetProperty(ref locale, value); Dirty(); } }
    public string Languages { get => languages; set { SetProperty(ref languages, value); Dirty(); } }
    public string Checker { get => checker; set { SetProperty(ref checker, value); Dirty(); } }
    public long TimeLimit { get => time; set { SetProperty(ref time, value); Dirty(); } }
    public long MemoryLimit { get => memory; set { SetProperty(ref memory, value); Dirty(); } }
    public long OutputLimit { get => output; set { SetProperty(ref output, value); Dirty(); } }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string? Error { get => error; private set => SetProperty(ref error, value); }
    public bool IsDirty { get; private set; }
    public void Load(ProblemDraftSummary value) { draft = value; title = value.Title; authors = string.Join(", ", value.Authors ?? []); tags = string.Join(", ", value.Tags ?? []); difficulty = value.Difficulty; locale = value.DefaultLocale; languages = string.Join(", ", value.AcceptedLanguages ?? ["cpp", "csharp"]); checker = value.Checker; time = value.TimeLimitMilliseconds; memory = value.MemoryLimitBytes; output = value.OutputLimitBytes; IsDirty = false; Status = $"Revision {value.Version}"; NotifyAll(); }
    public async Task SaveAsync(CancellationToken ct)
    {
        if (draft is null) return; Error = null;
        try { var result = await client.UpdateAsync(draft.Id, new(Title, Split(Authors), Split(Tags), Difficulty, DefaultLocale, Split(Languages), TimeLimit, MemoryLimit, OutputLimit, Checker, draft.Version), ct); if (result is not null) Load(result); Status = "Saved"; }
        catch (ApiException value) { Error = value.Problem.Title; Status = value.Problem.Code == "idempotency_conflict" ? "Revision conflict" : "Save failed"; }
    }
    private void Dirty() { IsDirty = true; Status = "Unsaved changes"; OnPropertyChanged(nameof(IsDirty)); }
    private static string[] Split(string value) => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    private void NotifyAll() { foreach (var name in new[] { nameof(Title), nameof(Authors), nameof(Tags), nameof(Difficulty), nameof(DefaultLocale), nameof(Languages), nameof(Checker), nameof(TimeLimit), nameof(MemoryLimit), nameof(OutputLimit), nameof(IsDirty) }) OnPropertyChanged(name); }
}
