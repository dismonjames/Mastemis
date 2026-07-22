using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Workers;

public sealed class WorkerOperationsViewModel : ObservableObject
{
    private readonly IWorkerInventoryClient client;
    private bool isBusy; private string? error; private string search = string.Empty;
    public WorkerOperationsViewModel(IWorkerInventoryClient client) { this.client = client; RefreshCommand = new AsyncCommand(RefreshAsync); }
    public ObservableCollection<WorkerInventoryItem> Workers { get; } = [];
    public ICommand RefreshCommand { get; }
    public string Search { get => search; set => SetProperty(ref search, value); }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string? Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error is not null;
    public bool IsEmpty => Workers.Count == 0;
    public int TotalCapacity => Workers.Sum(x => x.TotalCapacity);
    public int UsedCapacity => Workers.Sum(x => x.UsedCapacity);
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsBusy = true; Error = null;
        try { var page = await client.ListAsync(Search, null, cancellationToken).ConfigureAwait(true); Workers.Clear(); foreach (var item in page?.Items ?? []) Workers.Add(item); Notify(); }
        catch (ApiException value) { Error = value.Problem.Title; }
        finally { IsBusy = false; }
    }
    private void Notify() { OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(TotalCapacity)); OnPropertyChanged(nameof(UsedCapacity)); }
}
