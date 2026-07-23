using System.Collections.ObjectModel;
using System.Net;
using System.Text;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio.Tests;

public sealed class ProblemTestViewModel : ObservableObject
{
    private readonly IProblemTestClient client; private Guid problemId; private ProblemTestItem? selected;
    private string preview = "Select a test to load a bounded preview.", status = "Select a draft", error = string.Empty;
    public ProblemTestViewModel(IProblemTestClient client) { this.client = client; RefreshCommand = new AsyncCommand(RefreshAsync); PreviewInputCommand = new AsyncCommand(ct => PreviewAsync(false, ct)); PreviewOutputCommand = new AsyncCommand(ct => PreviewAsync(true, ct)); }
    public ObservableCollection<ProblemTestItem> Items { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand PreviewInputCommand { get; }
    public ICommand PreviewOutputCommand { get; }
    public ProblemTestItem? Selected { get => selected; set => SetProperty(ref selected, value); }
    public string Preview { get => preview; private set => SetProperty(ref preview, value); }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string Error { get => error; private set => SetProperty(ref error, value); }
    public void SetProblem(Guid id) { problemId = id; Items.Clear(); Preview = "Select a test to load a bounded preview."; }
    private async Task RefreshAsync(CancellationToken ct) => await RunAsync(async () => { var values = await client.ListAsync(problemId, ct); Items.Clear(); foreach (var value in values.Take(200)) Items.Add(value); Status = $"{values.Count} tests"; });
    private async Task PreviewAsync(bool output, CancellationToken ct) => await RunAsync(async () => { if (Selected is null) return; await using var stream = output ? await client.OpenOutputAsync(problemId, Selected.TestIndex, ct) : await client.OpenInputAsync(problemId, Selected.TestIndex, ct); using var reader = new StreamReader(stream, Encoding.UTF8); var buffer = new char[8192]; var read = await reader.ReadBlockAsync(buffer, ct); Preview = new string(buffer, 0, read) + (read == buffer.Length ? "\n… preview truncated" : string.Empty); });
    private async Task RunAsync(Func<Task> action) { Error = string.Empty; try { await action(); } catch (ApiException ex) { Error = ex.Problem.Status == HttpStatusCode.Forbidden ? "Hidden test content is locked for this account." : ex.Problem.Title; } }
}
