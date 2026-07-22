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
    public ExaminationViewModel(IExaminationClient client)
    {
        this.client = client;
        CreateCommand = new AsyncCommand(CreateAsync);
        RefreshCommand = new AsyncCommand(RefreshAsync);
        OpenCommand = new AsyncCommand(ct => TransitionAsync("open", ct));
        CloseCommand = new AsyncCommand(ct => TransitionAsync("close", ct));
    }
    public ICommand CreateCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand CloseCommand { get; }
    public string Title { get => title; set => SetProperty(ref title, value); }
    public string ExamId { get => examId; set => SetProperty(ref examId, value); }
    public ExaminationSummary? Examination { get => examination; private set { if (SetProperty(ref examination, value)) OnPropertyChanged(nameof(HasExamination)); } }
    public bool HasExamination => Examination is not null;
    public string? Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error is not null;
    private async Task CreateAsync(CancellationToken ct) => await RunAsync(async () => { Examination = await client.CreateAsync(Title, ct); ExamId = Examination.Id.ToString("D"); }).ConfigureAwait(true);
    private async Task RefreshAsync(CancellationToken ct) => await RunAsync(async () => { if (Guid.TryParse(ExamId, out var id)) Examination = await client.GetSummaryAsync(id, ct); else Error = "Enter a valid examination identifier."; }).ConfigureAwait(true);
    private async Task TransitionAsync(string action, CancellationToken ct) => await RunAsync(async () => { if (Examination is null) return; await client.TransitionAsync(Examination.Id, action, ct); Examination = await client.GetSummaryAsync(Examination.Id, ct); }).ConfigureAwait(true);
    private async Task RunAsync(Func<Task> action) { Error = null; try { await action().ConfigureAwait(true); } catch (ApiException value) { Error = value.Problem.Title; } }
}
