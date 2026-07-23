using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;
using Mastemis.Client.Core.Platform.Files;

namespace Mastemis.Client.Core.Features.ProblemStudio.Assets;

public sealed class ProblemAssetViewModel : ObservableObject
{
    private readonly IProblemAssetClient client; private readonly IClientFileService files;
    private Guid problemId; private ProblemAssetItem? selected; private ClientFile? pending;
    private string logicalName = string.Empty, status = "Select a draft", error = string.Empty;
    public ProblemAssetViewModel(IProblemAssetClient client, IClientFileService files) { this.client = client; this.files = files; RefreshCommand = new AsyncCommand(RefreshAsync); PickCommand = new AsyncCommand(PickAsync); UploadCommand = new AsyncCommand(UploadAsync); DownloadCommand = new AsyncCommand(DownloadAsync); DeleteCommand = new AsyncCommand(DeleteAsync); }
    public ObservableCollection<ProblemAssetItem> Items { get; } = [];
    public ICommand RefreshCommand { get; } public ICommand PickCommand { get; } public ICommand UploadCommand { get; } public ICommand DownloadCommand { get; } public ICommand DeleteCommand { get; }
    public ProblemAssetItem? Selected { get => selected; set => SetProperty(ref selected, value); }
    public string LogicalName { get => logicalName; set => SetProperty(ref logicalName, value); }
    public string PendingFile => pending?.Name ?? "No file selected";
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string Error { get => error; private set => SetProperty(ref error, value); }
    public void SetProblem(Guid id) { problemId = id; Items.Clear(); pending = null; Status = "Ready"; OnPropertyChanged(nameof(PendingFile)); }
    public async Task RefreshAsync(CancellationToken ct) => await RunAsync(async () => { var values = await client.ListAsync(problemId, ct); Items.Clear(); foreach (var value in values) Items.Add(value); Status = $"{values.Count} assets"; });
    private async Task PickAsync(CancellationToken ct) { pending = await files.PickOpenAsync([".png", ".jpg", ".jpeg", ".svg", ".txt", ".pdf"], ct); if (pending is not null) LogicalName = pending.Name; OnPropertyChanged(nameof(PendingFile)); }
    private async Task UploadAsync(CancellationToken ct) => await RunAsync(async () => { if (pending is null || problemId == Guid.Empty) throw new InvalidOperationException("Select a draft and a file."); await using var stream = await pending.OpenReadAsync(ct); await client.UploadAsync(problemId, LogicalName, pending.ContentType, stream, ct); pending = null; OnPropertyChanged(nameof(PendingFile)); await RefreshAsync(ct); });
    private async Task DownloadAsync(CancellationToken ct) => await RunAsync(async () => { if (Selected is null) return; await using var stream = await client.DownloadAsync(problemId, Selected.Id, ct); await files.SaveAsync(Selected.LogicalName, stream, ct); Status = "Asset saved"; });
    private async Task DeleteAsync(CancellationToken ct) => await RunAsync(async () => { if (Selected is null) return; await client.DeleteAsync(problemId, Selected.Id, ct); await RefreshAsync(ct); });
    private async Task RunAsync(Func<Task> action) { Error = string.Empty; try { await action(); } catch (ApiException ex) { Error = ex.Problem.Title; } catch (Exception ex) when (ex is InvalidOperationException or IOException) { Error = ex.Message; } }
}
