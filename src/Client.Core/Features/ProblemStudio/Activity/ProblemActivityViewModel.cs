using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio.Activity;

public sealed class ProblemActivityViewModel : ObservableObject
{
    private readonly IProblemActivityClient client; private Guid problemId; private int offset; private string filter = string.Empty;
    private bool hasMore; private string status = "Select a draft", error = string.Empty;
    public ProblemActivityViewModel(IProblemActivityClient client) { this.client = client; RefreshCommand = new AsyncCommand(RefreshAsync); MoreCommand = new AsyncCommand(MoreAsync); }
    public ObservableCollection<ProblemActivityItem> Items { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand MoreCommand { get; }
    public string Filter { get => filter; set => SetProperty(ref filter, value); }
    public bool HasMore { get => hasMore; private set => SetProperty(ref hasMore, value); }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string Error { get => error; private set => SetProperty(ref error, value); }
    public void SetProblem(Guid id) { problemId = id; offset = 0; Items.Clear(); Status = "Ready"; }
    private async Task RefreshAsync(CancellationToken ct) { offset = 0; Items.Clear(); await LoadAsync(ct); }
    private async Task MoreAsync(CancellationToken ct) { if (HasMore) { offset = Items.Count; await LoadAsync(ct); } }
    private async Task LoadAsync(CancellationToken ct) { Error = string.Empty; try { var page = await client.ListAsync(problemId, offset, 50, string.IsNullOrWhiteSpace(Filter) ? null : Filter, ct); foreach (var item in page?.Items ?? []) Items.Add(item); HasMore = page?.HasMore ?? false; Status = $"{Items.Count} activity entries"; } catch (ApiException ex) { Error = ex.Problem.Title; } }
}
