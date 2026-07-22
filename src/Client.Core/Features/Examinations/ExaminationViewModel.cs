using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Examinations;

public sealed class ExaminationViewModel : ObservableObject
{
    private readonly IExaminationClient client;
    private string title = string.Empty;
    private string examId = string.Empty;
    private ExaminationSummary? examination;
    private string? error;
    private bool isBusy;
    private DateTimeOffset startsAt = DateTimeOffset.Now.AddHours(1);
    private DateTimeOffset endsAt = DateTimeOffset.Now.AddHours(3);
    private string search = string.Empty;
    private string status = string.Empty;
    public ExaminationViewModel(IExaminationClient client)
    {
        this.client = client;
        CreateCommand = new AsyncCommand(CreateAsync);
        RefreshCommand = new AsyncCommand(RefreshAsync);
        OpenCommand = new AsyncCommand(ct => TransitionAsync("open", ct));
        CloseCommand = new AsyncCommand(ct => TransitionAsync("close", ct));
        CancelCommand = new AsyncCommand(ct => TransitionAsync("cancel", ct));
        ScheduleCommand = new AsyncCommand(ScheduleAsync);
        ListCommand = new AsyncCommand(ListAsync);
    }
    public ICommand CreateCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ScheduleCommand { get; }
    public ICommand ListCommand { get; }
    public ObservableCollection<ExaminationListItem> Examinations { get; } = [];
    public string Search { get => search; set => SetProperty(ref search, value); }
    public string Status { get => status; set => SetProperty(ref status, value); }
    public bool HasItems => Examinations.Count > 0;
    public string Title { get => title; set => SetProperty(ref title, value); }
    public string ExamId { get => examId; set => SetProperty(ref examId, value); }
    public ExaminationSummary? Examination { get => examination; private set { if (SetProperty(ref examination, value)) OnPropertyChanged(nameof(HasExamination)); } }
    public bool HasExamination => Examination is not null;
    public string? Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error is not null;
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public DateTimeOffset StartsAt { get => startsAt; set => SetProperty(ref startsAt, value); }
    public DateTimeOffset EndsAt { get => endsAt; set => SetProperty(ref endsAt, value); }
    private async Task CreateAsync(CancellationToken ct) => await RunAsync(async () => { Examination = await client.CreateAsync(Title, ct); ExamId = Examination.Id.ToString("D"); }).ConfigureAwait(true);
    private async Task RefreshAsync(CancellationToken ct) => await RunAsync(async () => { if (Guid.TryParse(ExamId, out var id)) Examination = await client.GetSummaryAsync(id, ct); else Error = "Enter a valid examination identifier."; }).ConfigureAwait(true);
    private async Task TransitionAsync(string action, CancellationToken ct) => await RunAsync(async () => { if (Examination is null) return; await client.TransitionAsync(Examination.Id, action, ct); Examination = await client.GetSummaryAsync(Examination.Id, ct); }).ConfigureAwait(true);
    private async Task ScheduleAsync(CancellationToken ct) => await RunAsync(async () =>
    {
        if (Examination is null) { Error = "Load an examination before scheduling."; return; }
        if (EndsAt <= StartsAt) { Error = "The end time must be after the start time."; return; }
        await client.ScheduleAsync(Examination.Id, StartsAt.ToUniversalTime(), EndsAt.ToUniversalTime(), ct);
        Examination = await client.GetSummaryAsync(Examination.Id, ct);
    }).ConfigureAwait(true);
    private async Task ListAsync(CancellationToken ct) => await RunAsync(async () =>
    {
        var page = await client.ListAsync(Search, Status, 0, ct).ConfigureAwait(true);
        Examinations.Clear(); foreach (var item in page?.Items ?? []) Examinations.Add(item);
        OnPropertyChanged(nameof(HasItems));
    }).ConfigureAwait(true);
    private async Task RunAsync(Func<Task> action) { Error = null; IsBusy = true; try { await action().ConfigureAwait(true); } catch (ApiException value) { Error = value.Problem.Title; } finally { IsBusy = false; } }
}
