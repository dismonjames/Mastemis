using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Features.ProblemStudio;
using Mastemis.Client.Core.Networking.Http;
using Mastemis.Client.Core.Platform.Files;

namespace Mastemis.Client.Core.Features.ProblemStudio.Packages;

public sealed class ProblemPackageViewModel : ObservableObject
{
    private readonly IProblemPackageClient client; private readonly IClientFileService files; private Guid problemId; private int revision;
    private ClientFile? pending; private PackageExportMetadata? selectedExport; private string status = "Select a draft", error = string.Empty;
    public ProblemPackageViewModel(IProblemPackageClient client, IClientFileService files) { this.client = client; this.files = files; PickCommand = new AsyncCommand(PickAsync); ValidateCommand = new AsyncCommand(ValidateAsync); ReplaceCommand = new AsyncCommand(ReplaceAsync); ExportCommand = new AsyncCommand(ExportAsync); RefreshCommand = new AsyncCommand(RefreshAsync); DownloadCommand = new AsyncCommand(DownloadAsync); ExpireCommand = new AsyncCommand(ExpireAsync); }
    public ObservableCollection<PackageDiagnostic> Diagnostics { get; } = [];
    public ObservableCollection<PackageImportMetadata> Imports { get; } = [];
    public ObservableCollection<PackageExportMetadata> Exports { get; } = [];
    public ICommand PickCommand { get; } public ICommand ValidateCommand { get; } public ICommand ReplaceCommand { get; } public ICommand ExportCommand { get; } public ICommand RefreshCommand { get; } public ICommand DownloadCommand { get; } public ICommand ExpireCommand { get; }
    public PackageExportMetadata? SelectedExport { get => selectedExport; set => SetProperty(ref selectedExport, value); }
    public string PendingFile => pending?.Name ?? "No .mas package selected";
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string Error { get => error; private set => SetProperty(ref error, value); }
    public void SetProblem(Guid id, int version) { problemId = id; revision = version; pending = null; Diagnostics.Clear(); OnPropertyChanged(nameof(PendingFile)); }
    private async Task PickAsync(CancellationToken ct) { pending = await files.PickOpenAsync([".mas"], ct); OnPropertyChanged(nameof(PendingFile)); }
    private async Task ValidateAsync(CancellationToken ct) => await WithPackageAsync(async stream => { var result = await client.ValidateAsync(stream, ct); Diagnostics.Clear(); foreach (var value in result.Diagnostics) Diagnostics.Add(value); Status = result.Diagnostics.Count == 0 ? "Package is valid" : $"{result.Diagnostics.Count} diagnostics"; });
    private async Task ReplaceAsync(CancellationToken ct) => await WithPackageAsync(async stream => { await client.ReplaceDraftAsync(problemId, revision, stream, Guid.NewGuid().ToString("N"), ct); Status = "Draft replaced"; await RefreshAsync(ct); });
    private async Task ExportAsync(CancellationToken ct) => await RunAsync(async () => { await client.ExportAsync(problemId, Guid.NewGuid().ToString("N"), ct); await RefreshAsync(ct); Status = "Export created"; });
    private async Task RefreshAsync(CancellationToken ct) => await RunAsync(async () => { var imports = await client.ListImportsAsync(problemId, ct); var exports = await client.ListExportsAsync(problemId, ct); Imports.Clear(); Exports.Clear(); foreach (var item in imports) Imports.Add(item); foreach (var item in exports) Exports.Add(item); Status = $"{imports.Count} imports · {exports.Count} exports"; });
    private async Task DownloadAsync(CancellationToken ct) => await RunAsync(async () => { if (SelectedExport is null) return; await using var stream = await client.DownloadAsync(problemId, SelectedExport.ExportId, ct); await files.SaveAsync($"{problemId:N}.mas", stream, ct); Status = "Package saved"; });
    private async Task ExpireAsync(CancellationToken ct) => await RunAsync(async () => { if (SelectedExport is null) return; await client.ExpireAsync(problemId, SelectedExport.ExportId, ct); await RefreshAsync(ct); });
    private async Task WithPackageAsync(Func<Stream, Task> action) { if (pending is null) { Error = "Select a .mas package first."; return; } await RunAsync(async () => { await using var stream = await pending.OpenReadAsync(CancellationToken.None); await action(stream); }); }
    private async Task RunAsync(Func<Task> action) { Error = string.Empty; try { await action(); } catch (ApiException ex) { Error = ex.Problem.Title; } catch (Exception ex) when (ex is IOException or InvalidOperationException) { Error = ex.Message; } }
}
