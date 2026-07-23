using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.ProblemStudio.Overview;

public sealed class ProblemOverviewViewModel : ObservableObject
{
    private readonly IProblemOverviewClient client; private Guid problemId; private ProblemOverviewData? data; private string error = string.Empty;
    public ProblemOverviewViewModel(IProblemOverviewClient client) { this.client = client; RefreshCommand = new AsyncCommand(RefreshAsync); }
    public ICommand RefreshCommand { get; }
    public ProblemOverviewData? Data { get => data; private set => SetProperty(ref data, value); }
    public string Error { get => error; private set => SetProperty(ref error, value); }
    public void SetProblem(Guid id) { problemId = id; Data = null; }
    private async Task RefreshAsync(CancellationToken ct) { Error = string.Empty; try { Data = await client.GetAsync(problemId, ct); } catch (ApiException ex) { Error = ex.Problem.Title; } }
}
