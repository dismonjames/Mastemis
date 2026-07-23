using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Features.ProblemStudio.Activity;
using Mastemis.Client.Core.Features.ProblemStudio.Assets;
using Mastemis.Client.Core.Features.ProblemStudio.Metadata;
using Mastemis.Client.Core.Features.ProblemStudio.Overview;
using Mastemis.Client.Core.Features.ProblemStudio.Packages;
using Mastemis.Client.Core.Features.ProblemStudio.Permissions;
using Mastemis.Client.Core.Features.ProblemStudio.ReferenceSolution;
using Mastemis.Client.Core.Features.ProblemStudio.Statements;
using Mastemis.Client.Core.Features.ProblemStudio.Tests;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio;

public sealed class ProblemStudioViewModel : ObservableObject
{
    private readonly IProblemDraftClient draftsClient; private readonly IProblemMasClient masClient; private readonly IProblemGenerationClient generationClient;
    private ProblemDraftSummary? selectedDraft; private string source = string.Empty; private string seed = "1"; private string status = "Ready"; private string? error;
    private Guid? operationId; private GenerationProgress? progress;
    private int cursorLine = 1, cursorColumn = 1;
    public ProblemStudioViewModel(IProblemDraftClient draftsClient, IProblemMasClient masClient, IProblemGenerationClient generationClient,
        ProblemMetadataViewModel metadata, StatementAuthoringViewModel statements, ProblemAssetViewModel assets,
        ReferenceSolutionViewModel referenceSolution, ProblemTestViewModel tests, ProblemPackageViewModel packages,
        ProblemPermissionViewModel permissions, ProblemActivityViewModel activity, ProblemOverviewViewModel overview)
    {
        this.draftsClient = draftsClient; this.masClient = masClient; this.generationClient = generationClient;
        Metadata = metadata; Statements = statements; Assets = assets; ReferenceSolution = referenceSolution;
        Tests = tests; Packages = packages; Permissions = permissions; Activity = activity; Overview = overview;
        RefreshCommand = new AsyncCommand(RefreshAsync); LoadCommand = new AsyncCommand(LoadAsync); SaveMasCommand = new AsyncCommand(SaveMasAsync); ValidateCommand = new AsyncCommand(ValidateAsync); GenerateCommand = new AsyncCommand(GenerateAsync);
        RefreshGenerationCommand = new AsyncCommand(RefreshGenerationAsync); CancelGenerationCommand = new AsyncCommand(CancelGenerationAsync);
    }
    public ObservableCollection<ProblemDraftSummary> Drafts { get; } = [];
    public ObservableCollection<MasDiagnostic> Diagnostics { get; } = [];
    public ProblemMetadataViewModel Metadata { get; }
    public StatementAuthoringViewModel Statements { get; }
    public ProblemAssetViewModel Assets { get; }
    public ReferenceSolutionViewModel ReferenceSolution { get; }
    public ProblemTestViewModel Tests { get; }
    public ProblemPackageViewModel Packages { get; }
    public ProblemPermissionViewModel Permissions { get; }
    public ProblemActivityViewModel Activity { get; }
    public ProblemOverviewViewModel Overview { get; }
    public ICommand RefreshCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand SaveMasCommand { get; }
    public ICommand ValidateCommand { get; }
    public ICommand GenerateCommand { get; }
    public ICommand RefreshGenerationCommand { get; }
    public ICommand CancelGenerationCommand { get; }
    public ProblemDraftSummary? SelectedDraft { get => selectedDraft; set => SetProperty(ref selectedDraft, value); }
    public string Source { get => source; set => SetProperty(ref source, value); }
    public string Seed { get => seed; set => SetProperty(ref seed, value); }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string? Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error is not null;
    public GenerationProgress? Progress { get => progress; private set => SetProperty(ref progress, value); }
    public string ProgressText => Progress is null ? "No generation operation selected" : $"{Progress.Status} · {Progress.Numerator}/{Progress.Denominator}";
    public string ReferenceStatus => Progress?.ReferenceJobStatus ?? "Not queued";
    public string CursorPosition => $"Ln {cursorLine}, Col {cursorColumn}";
    public void SetCursor(int line, int column) { cursorLine = Math.Max(1, line); cursorColumn = Math.Max(1, column); OnPropertyChanged(nameof(CursorPosition)); }
    private async Task RefreshAsync(CancellationToken ct) => await RunAsync(async () => { var values = await draftsClient.ListAsync(ct); Drafts.Clear(); foreach (var value in values) Drafts.Add(value); Status = $"{values.Count} authorized drafts"; }).ConfigureAwait(true);
    private async Task LoadAsync(CancellationToken ct) => await RunAsync(async () => { if (SelectedDraft is null) return; var detailed = await draftsClient.GetAsync(SelectedDraft.Id, ct) ?? SelectedDraft; SelectedDraft = detailed; Metadata.Load(detailed); Statements.SetProblem(detailed.Id); Assets.SetProblem(detailed.Id); ReferenceSolution.SetProblem(detailed.Id); Tests.SetProblem(detailed.Id); Packages.SetProblem(detailed.Id, detailed.Version); Permissions.SetProblem(detailed.Id); Activity.SetProblem(detailed.Id); Overview.SetProblem(detailed.Id); var value = await masClient.GetAsync(detailed.Id, ct); Source = value?.Source ?? string.Empty; Status = value is null ? "No MAS source" : $"MAS revision {value.Revision}"; }).ConfigureAwait(true);
    private async Task SaveMasAsync(CancellationToken ct) => await RunAsync(async () => { if (SelectedDraft is null) return; var current = await masClient.GetAsync(SelectedDraft.Id, ct); await masClient.UpdateAsync(SelectedDraft.Id, Source, current?.Revision ?? 0, ct); Status = "MAS source saved"; }).ConfigureAwait(true);
    private async Task ValidateAsync(CancellationToken ct) => await RunAsync(async () => { if (SelectedDraft is null) return; var values = await masClient.ValidateAsync(SelectedDraft.Id, Source, ct); Diagnostics.Clear(); foreach (var value in values) Diagnostics.Add(value); Status = values.Count == 0 ? "Validation passed" : $"{values.Count} diagnostics"; }).ConfigureAwait(true);
    private async Task GenerateAsync(CancellationToken ct) => await RunAsync(async () => { if (SelectedDraft is null || !ulong.TryParse(Seed, out var parsed)) { Error = "Select a draft and enter a valid seed."; return; } var operation = await generationClient.StartAsync(SelectedDraft.Id, parsed, ct); operationId = operation.Id; Status = $"Generation {operation.Status} · {operation.Id:D}"; await RefreshGenerationAsync(ct); }).ConfigureAwait(true);
    private async Task RefreshGenerationAsync(CancellationToken ct) => await RunAsync(async () =>
    {
        if (SelectedDraft is null || operationId is null) return;
        Progress = await generationClient.GetProgressAsync(SelectedDraft.Id, operationId.Value, ct);
        var values = await generationClient.GetDiagnosticsAsync(SelectedDraft.Id, operationId.Value, ct);
        Diagnostics.Clear(); foreach (var value in values) Diagnostics.Add(new(value.Code, "Operation", value.Message));
        OnPropertyChanged(nameof(ProgressText)); OnPropertyChanged(nameof(ReferenceStatus));
    }).ConfigureAwait(true);
    private async Task CancelGenerationAsync(CancellationToken ct) => await RunAsync(async () =>
    {
        if (SelectedDraft is null || operationId is null) return;
        await generationClient.CancelAsync(SelectedDraft.Id, operationId.Value, ct);
        Status = "Generation cancellation requested";
        await RefreshGenerationAsync(ct);
    }).ConfigureAwait(true);
    private async Task RunAsync(Func<Task> action) { Error = null; try { await action().ConfigureAwait(true); } catch (ApiException value) { Error = value.Problem.Title; } }
}
