using System.Collections.ObjectModel;
using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio.Permissions;

public sealed class ProblemPermissionViewModel : ObservableObject
{
    private readonly IProblemPermissionClient client; private Guid problemId; private ProblemPermissionItem? selected;
    private string userId = string.Empty, role = "Viewer", status = "Select a draft", error = string.Empty;
    public ProblemPermissionViewModel(IProblemPermissionClient client) { this.client = client; RefreshCommand = new AsyncCommand(RefreshAsync); AssignCommand = new AsyncCommand(AssignAsync); RevokeCommand = new AsyncCommand(RevokeAsync); }
    public ObservableCollection<ProblemPermissionItem> Items { get; } = [];
    public ICommand RefreshCommand { get; } public ICommand AssignCommand { get; } public ICommand RevokeCommand { get; }
    public ProblemPermissionItem? Selected { get => selected; set => SetProperty(ref selected, value); }
    public string UserId { get => userId; set => SetProperty(ref userId, value); }
    public string Role { get => role; set => SetProperty(ref role, value); }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string Error { get => error; private set => SetProperty(ref error, value); }
    public void SetProblem(Guid id) { problemId = id; Items.Clear(); Status = "Ready"; }
    private async Task RefreshAsync(CancellationToken ct) => await RunAsync(async () => { var values = await client.ListAsync(problemId, ct); Items.Clear(); foreach (var item in values) Items.Add(item); Status = $"{values.Count} assignments"; });
    private async Task AssignAsync(CancellationToken ct) => await RunAsync(async () => { if (!Guid.TryParse(UserId, out var id)) throw new InvalidOperationException("Enter a valid user identifier."); await client.AssignAsync(problemId, id, Role, null, ct); await RefreshAsync(ct); });
    private async Task RevokeAsync(CancellationToken ct) => await RunAsync(async () => { if (Selected is null) return; await client.RevokeAsync(problemId, Selected.UserId, ct); await RefreshAsync(ct); });
    private async Task RunAsync(Func<Task> action) { Error = string.Empty; try { await action(); } catch (ApiException ex) { Error = ex.Problem.Title; } catch (InvalidOperationException ex) { Error = ex.Message; } }
}
