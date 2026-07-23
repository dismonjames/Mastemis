using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio.Permissions;

public sealed class ProblemPermissionViewModel : ObservableObject
{
    private readonly IProblemPermissionClient client; private Guid problemId; private ProblemPermissionItem? selected;
    private string userId = string.Empty, role = "Viewer", status = "Select a draft", error = string.Empty, expiration = string.Empty, examId = string.Empty;
    private ProblemExamAssignmentItem? selectedExam;
    public ProblemPermissionViewModel(IProblemPermissionClient client) { this.client = client; RefreshCommand = new AsyncCommand(RefreshAsync); AssignCommand = new AsyncCommand(AssignAsync); RevokeCommand = new AsyncCommand(RevokeAsync); AssignExamCommand = new AsyncCommand(AssignExamAsync); RemoveExamCommand = new AsyncCommand(RemoveExamAsync); }
    public ObservableCollection<ProblemPermissionItem> Items { get; } = [];
    public ObservableCollection<ProblemExamAssignmentItem> Exams { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand AssignCommand { get; }
    public ICommand RevokeCommand { get; }
    public ICommand AssignExamCommand { get; }
    public ICommand RemoveExamCommand { get; }
    public ProblemPermissionItem? Selected { get => selected; set => SetProperty(ref selected, value); }
    public string UserId { get => userId; set => SetProperty(ref userId, value); }
    public string Role { get => role; set => SetProperty(ref role, value); }
    public string Expiration { get => expiration; set => SetProperty(ref expiration, value); }
    public string ExamId { get => examId; set => SetProperty(ref examId, value); }
    public ProblemExamAssignmentItem? SelectedExam { get => selectedExam; set => SetProperty(ref selectedExam, value); }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string Error { get => error; private set => SetProperty(ref error, value); }
    public void SetProblem(Guid id) { problemId = id; Items.Clear(); Status = "Ready"; }
    private async Task RefreshAsync(CancellationToken ct) => await RunAsync(async () => { var values = await client.ListAsync(problemId, ct); var exams = await client.ListExamsAsync(problemId, ct); Items.Clear(); Exams.Clear(); foreach (var item in values) Items.Add(item); foreach (var item in exams) Exams.Add(item); Status = $"{values.Count} assignments · {exams.Count} examinations"; });
    private async Task AssignAsync(CancellationToken ct) => await RunAsync(async () => { if (!Guid.TryParse(UserId, out var id)) throw new InvalidOperationException("Enter a valid user identifier."); DateTimeOffset? expires = null; if (!string.IsNullOrWhiteSpace(Expiration)) { if (!DateTimeOffset.TryParse(Expiration, out var parsed) || parsed <= DateTimeOffset.UtcNow) throw new InvalidOperationException("Expiration must be a future date and time."); expires = parsed.ToUniversalTime(); } await client.AssignAsync(problemId, id, Role, expires, ct); await RefreshAsync(ct); });
    private async Task RevokeAsync(CancellationToken ct) => await RunAsync(async () => { if (Selected is null) return; await client.RevokeAsync(problemId, Selected.UserId, ct); await RefreshAsync(ct); });
    private async Task AssignExamAsync(CancellationToken ct) => await RunAsync(async () => { if (!Guid.TryParse(ExamId, out var id)) throw new InvalidOperationException("Enter a valid examination identifier."); await client.AssignExamAsync(problemId, id, ct); await RefreshAsync(ct); });
    private async Task RemoveExamAsync(CancellationToken ct) => await RunAsync(async () => { if (SelectedExam is null) return; await client.RemoveExamAsync(problemId, SelectedExam.ExamId, ct); await RefreshAsync(ct); });
    private async Task RunAsync(Func<Task> action) { Error = string.Empty; try { await action(); } catch (ApiException ex) { Error = ex.Problem.Title; } catch (InvalidOperationException ex) { Error = ex.Message; } }
}
