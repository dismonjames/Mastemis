using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio.Statements;

public sealed class StatementAuthoringViewModel : ObservableObject
{
    private readonly IProblemStatementClient client; private Guid? problemId; private int? revision;
    private StatementSummary? selected; private string locale = "en", title = string.Empty, markdown = string.Empty;
    private string input = string.Empty, output = string.Empty, constraints = string.Empty, notes = string.Empty, status = "No statement loaded"; private string? error;
    public StatementAuthoringViewModel(IProblemStatementClient client) { this.client = client; RefreshCommand = new AsyncCommand(RefreshAsync); LoadCommand = new AsyncCommand(LoadAsync); SaveCommand = new AsyncCommand(SaveAsync); DeleteCommand = new AsyncCommand(DeleteAsync); }
    public ObservableCollection<StatementSummary> Locales { get; } = [];
    public ICommand RefreshCommand { get; } public ICommand LoadCommand { get; } public ICommand SaveCommand { get; } public ICommand DeleteCommand { get; }
    public StatementSummary? Selected { get => selected; set => SetProperty(ref selected, value); } public string Locale { get => locale; set => SetProperty(ref locale, value); }
    public string Title { get => title; set => SetProperty(ref title, value); } public string Markdown { get => markdown; set => SetProperty(ref markdown, value); }
    public string InputDescription { get => input; set => SetProperty(ref input, value); } public string OutputDescription { get => output; set => SetProperty(ref output, value); }
    public string Constraints { get => constraints; set => SetProperty(ref constraints, value); } public string Notes { get => notes; set => SetProperty(ref notes, value); }
    public string Status { get => status; private set => SetProperty(ref status, value); } public string? Error { get => error; private set => SetProperty(ref error, value); }
    public void SetProblem(Guid id) { problemId = id; revision = null; Locales.Clear(); Status = "Load localized statements"; }
    private async Task RefreshAsync(CancellationToken ct) { if (problemId is null) return; await Run(async () => { var values = await client.ListAsync(problemId.Value, ct); Locales.Clear(); foreach (var value in values) Locales.Add(value); Status = $"{values.Count} locale(s)"; }); }
    private async Task LoadAsync(CancellationToken ct) { if (problemId is null || Selected is null) return; await Run(async () => { var value = await client.GetAsync(problemId.Value, Selected.Locale, ct); if (value is null) return; Locale = value.Locale; revision = value.Revision; Title = value.Content.Title; Markdown = value.Content.Markdown; InputDescription = value.Content.InputDescription; OutputDescription = value.Content.OutputDescription; Constraints = value.Content.Constraints; Notes = value.Content.Notes; Status = $"Revision {value.Revision}"; }); }
    private async Task SaveAsync(CancellationToken ct) { if (problemId is null) return; await Run(async () => { var value = await client.SaveAsync(problemId.Value, Locale, new(Title, Markdown, InputDescription, OutputDescription, Constraints, Notes), revision, ct); revision = value?.Revision; Status = "Saved"; await RefreshAsync(ct); }); }
    private async Task DeleteAsync(CancellationToken ct) { if (problemId is null || Selected is null) return; await Run(async () => { await client.DeleteAsync(problemId.Value, Selected.Locale, ct); revision = null; await RefreshAsync(ct); }); }
    private async Task Run(Func<Task> action) { Error = null; try { await action(); } catch (ApiException value) { Error = value.Problem.Title; Status = "Request failed"; } }
}
