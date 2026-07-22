using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Features.ProblemStudio;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Problems;

public sealed class ProblemLibraryViewModel : ObservableObject
{
    private readonly IProblemDraftClient client;
    private string search = string.Empty;
    private bool isBusy;
    private string? error;

    public ProblemLibraryViewModel(IProblemDraftClient client)
    {
        this.client = client;
        RefreshCommand = new AsyncCommand(RefreshAsync);
        CreateCommand = new AsyncCommand(CreateAsync);
    }

    public ObservableCollection<ProblemDraftSummary> Problems { get; } = [];
    public ObservableCollection<ProblemDraftSummary> VisibleProblems { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand CreateCommand { get; }
    public string NewTitle { get; set; } = string.Empty;
    public string Search { get => search; set { if (SetProperty(ref search, value)) ApplyFilter(); } }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string? Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error is not null;
    public bool IsEmpty => VisibleProblems.Count == 0;

    private async Task RefreshAsync(CancellationToken cancellationToken) => await RunAsync(async () =>
    {
        var values = await client.ListAsync(cancellationToken).ConfigureAwait(true);
        Problems.Clear();
        foreach (var value in values) Problems.Add(value);
        ApplyFilter();
    }).ConfigureAwait(true);

    private async Task CreateAsync(CancellationToken cancellationToken) => await RunAsync(async () =>
    {
        var title = NewTitle.Trim();
        if (title.Length == 0) { Error = "Enter a problem title."; return; }
        await client.CreateAsync(title, "en", cancellationToken).ConfigureAwait(true);
        NewTitle = string.Empty;
        OnPropertyChanged(nameof(NewTitle));
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }).ConfigureAwait(true);

    private void ApplyFilter()
    {
        VisibleProblems.Clear();
        foreach (var problem in Problems.Where(x => string.IsNullOrWhiteSpace(Search) || x.Title.Contains(Search, StringComparison.OrdinalIgnoreCase)))
            VisibleProblems.Add(problem);
        OnPropertyChanged(nameof(IsEmpty));
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true; Error = null;
        try { await action().ConfigureAwait(true); }
        catch (ApiException value) { Error = value.Problem.Title; }
        finally { IsBusy = false; }
    }
}
