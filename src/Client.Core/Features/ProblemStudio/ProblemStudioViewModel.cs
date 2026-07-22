using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio;

public sealed class ProblemStudioViewModel : ObservableObject
{
    private readonly IProblemDraftClient draftsClient; private readonly IProblemMasClient masClient; private readonly IProblemGenerationClient generationClient;
    private ProblemDraftSummary? selectedDraft; private string source = string.Empty; private string seed = "1"; private string status = "Ready"; private string? error;
    public ProblemStudioViewModel(IProblemDraftClient draftsClient, IProblemMasClient masClient, IProblemGenerationClient generationClient)
    {
        this.draftsClient = draftsClient; this.masClient = masClient; this.generationClient = generationClient;
        RefreshCommand = new AsyncCommand(RefreshAsync); LoadCommand = new AsyncCommand(LoadAsync); SaveMasCommand = new AsyncCommand(SaveMasAsync); ValidateCommand = new AsyncCommand(ValidateAsync); GenerateCommand = new AsyncCommand(GenerateAsync);
    }
    public ObservableCollection<ProblemDraftSummary> Drafts { get; } = [];
    public ObservableCollection<MasDiagnostic> Diagnostics { get; } = [];
    public ICommand RefreshCommand { get; } public ICommand LoadCommand { get; } public ICommand SaveMasCommand { get; } public ICommand ValidateCommand { get; } public ICommand GenerateCommand { get; }
    public ProblemDraftSummary? SelectedDraft { get => selectedDraft; set => SetProperty(ref selectedDraft, value); }
    public string Source { get => source; set => SetProperty(ref source, value); }
    public string Seed { get => seed; set => SetProperty(ref seed, value); }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string? Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } } public bool HasError => Error is not null;
    private async Task RefreshAsync(CancellationToken ct) => await RunAsync(async () => { var values = await draftsClient.ListAsync(ct); Drafts.Clear(); foreach (var value in values) Drafts.Add(value); Status = $"{values.Count} authorized drafts"; }).ConfigureAwait(true);
    private async Task LoadAsync(CancellationToken ct) => await RunAsync(async () => { if (SelectedDraft is null) return; var value = await masClient.GetAsync(SelectedDraft.Id, ct); Source = value?.Source ?? string.Empty; Status = value is null ? "No MAS source" : $"MAS revision {value.Revision}"; }).ConfigureAwait(true);
    private async Task SaveMasAsync(CancellationToken ct) => await RunAsync(async () => { if (SelectedDraft is null) return; var current = await masClient.GetAsync(SelectedDraft.Id, ct); await masClient.UpdateAsync(SelectedDraft.Id, Source, current?.Revision ?? 0, ct); Status = "MAS source saved"; }).ConfigureAwait(true);
    private async Task ValidateAsync(CancellationToken ct) => await RunAsync(async () => { if (SelectedDraft is null) return; var values = await masClient.ValidateAsync(SelectedDraft.Id, Source, ct); Diagnostics.Clear(); foreach (var value in values) Diagnostics.Add(value); Status = values.Count == 0 ? "Validation passed" : $"{values.Count} diagnostics"; }).ConfigureAwait(true);
    private async Task GenerateAsync(CancellationToken ct) => await RunAsync(async () => { if (SelectedDraft is null || !ulong.TryParse(Seed, out var parsed)) { Error = "Select a draft and enter a valid seed."; return; } var operation = await generationClient.StartAsync(SelectedDraft.Id, parsed, ct); Status = $"Generation {operation.Status} · {operation.Id:D}"; }).ConfigureAwait(true);
    private async Task RunAsync(Func<Task> action) { Error = null; try { await action().ConfigureAwait(true); } catch (ApiException value) { Error = value.Problem.Title; } }
}
