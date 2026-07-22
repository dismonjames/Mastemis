using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Networking.Http;
using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Core.Features.Health;

public sealed class HealthViewModel : ObservableObject
{
    private readonly IServerProbe probe; private readonly ClientSession session; private string state = "Not checked"; private string details = string.Empty;
    public HealthViewModel(IServerProbe probe, ClientSession session) { this.probe = probe; this.session = session; RefreshCommand = new AsyncCommand(RefreshAsync); }
    public ICommand RefreshCommand { get; } public string State { get => state; private set => SetProperty(ref state, value); } public string Details { get => details; private set => SetProperty(ref details, value); }
    private async Task RefreshAsync(CancellationToken ct) { var uri = session.ServerBaseUri; if (uri is null) return; var result = await probe.ProbeAsync(uri, ct).ConfigureAwait(true); State = result.IsReady ? "Ready" : result.IsAvailable ? "Live, not ready" : "Unavailable"; Details = result.Error ?? $"Server version {result.Version ?? "unknown"}. Readiness covers PostgreSQL, queues, storage, outbox, and reconciliation."; }
}
