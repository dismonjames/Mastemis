using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Evidence;

public sealed class EvidenceViewModel : ObservableObject
{
    private readonly IEvidenceClient client;
    private EvidencePackageItem? selectedPackage;
    private string? error;

    public EvidenceViewModel(IEvidenceClient client)
    {
        this.client = client;
        RefreshCommand = new AsyncCommand(RefreshAsync);
        InspectCommand = new AsyncCommand(InspectAsync);
    }

    public ObservableCollection<EvidencePackageItem> Packages { get; } = [];
    public ObservableCollection<EvidenceTimelineItem> Timeline { get; } = [];
    public ObservableCollection<EvidenceAuditItem> Audit { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand InspectCommand { get; }
    public EvidencePackageItem? SelectedPackage { get => selectedPackage; set => SetProperty(ref selectedPackage, value); }
    public string? Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error is not null;
    public bool IsEmpty => Packages.Count == 0;

    private async Task RefreshAsync(CancellationToken cancellationToken) => await RunAsync(async () =>
    {
        var values = await client.ListAsync(cancellationToken).ConfigureAwait(true);
        Packages.Clear(); foreach (var value in values) Packages.Add(value);
        OnPropertyChanged(nameof(IsEmpty));
    }).ConfigureAwait(true);

    private async Task InspectAsync(CancellationToken cancellationToken) => await RunAsync(async () =>
    {
        if (SelectedPackage is null) return;
        var timeline = await client.TimelineAsync(SelectedPackage.Id.Value, cancellationToken).ConfigureAwait(true);
        var audit = await client.AuditAsync(SelectedPackage.Id.Value, cancellationToken).ConfigureAwait(true);
        Timeline.Clear(); foreach (var value in timeline) Timeline.Add(value);
        Audit.Clear(); foreach (var value in audit) Audit.Add(value);
    }).ConfigureAwait(true);

    private async Task RunAsync(Func<Task> action)
    {
        Error = null;
        try { await action().ConfigureAwait(true); }
        catch (ApiException value) { Error = value.Problem.Title; }
    }
}
