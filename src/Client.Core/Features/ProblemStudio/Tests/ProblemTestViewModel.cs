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
    private ProblemTestSetItem? selectedVersion; private bool hasMore; private int offset;
    private string preview = "Select a test to load a bounded preview.", status = "Select a draft", error = string.Empty;
    public ProblemTestViewModel(IProblemTestClient client) { this.client = client; RefreshCommand = new AsyncCommand(RefreshAsync); MoreCommand = new AsyncCommand(MoreAsync); PreviewInputCommand = new AsyncCommand(ct => PreviewAsync(false, ct)); PreviewOutputCommand = new AsyncCommand(ct => PreviewAsync(true, ct)); }
    public ObservableCollection<ProblemTestItem> Items { get; } = [];
    public ObservableCollection<ProblemTestSetItem> Versions { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand PreviewInputCommand { get; }
    public ICommand PreviewOutputCommand { get; }
    public ICommand MoreCommand { get; }
    public ProblemTestItem? Selected { get => selected; set => SetProperty(ref selected, value); }
    public ProblemTestSetItem? SelectedVersion { get => selectedVersion; set { if (SetProperty(ref selectedVersion, value)) { offset = 0; Items.Clear(); } } }
    public bool HasMore { get => hasMore; private set => SetProperty(ref hasMore, value); }
    public string Preview { get => preview; private set => SetProperty(ref preview, value); }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string Error { get => error; private set => SetProperty(ref error, value); }
    public void SetProblem(Guid id) { problemId = id; Items.Clear(); Versions.Clear(); Preview = "Select a test to load a bounded preview."; }
    private async Task RefreshAsync(CancellationToken ct) => await RunAsync(async () => { var versions = await client.ListVersionsAsync(problemId, ct); Versions.Clear(); foreach (var value in versions) Versions.Add(value); SelectedVersion ??= Versions.FirstOrDefault(); offset = 0; Items.Clear(); await LoadPageAsync(ct); });
    private async Task MoreAsync(CancellationToken ct) => await RunAsync(async () => { if (HasMore) { offset = Items.Count; await LoadPageAsync(ct); } });
    private async Task LoadPageAsync(CancellationToken ct) { if (SelectedVersion is null) { Status = "No test set"; return; } var page = await client.ListPageAsync(problemId, SelectedVersion.TestSetId, offset, 50, ct); foreach (var value in page?.Items ?? []) Items.Add(value); HasMore = page?.HasMore ?? false; Status = $"Version {SelectedVersion.Version} · {Items.Count} loaded tests"; }
    private async Task PreviewAsync(bool output, CancellationToken ct) => await RunAsync(async () => { if (Selected is null) return; await using var stream = output ? await client.OpenOutputAsync(problemId, Selected.TestIndex, ct) : await client.OpenInputAsync(problemId, Selected.TestIndex, ct); using var reader = new StreamReader(stream, Encoding.UTF8); var buffer = new char[8192]; var read = await reader.ReadBlockAsync(buffer, ct); Preview = new string(buffer, 0, read) + (read == buffer.Length ? "\n… preview truncated" : string.Empty); });
    private async Task RunAsync(Func<Task> action) { Error = string.Empty; try { await action(); } catch (ApiException ex) { Error = ex.Problem.Status == HttpStatusCode.Forbidden ? "Hidden test content is locked for this account." : ex.Problem.Title; } }
}
