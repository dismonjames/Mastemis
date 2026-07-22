using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Core.Features.Workers;

public sealed record WorkerInventoryItem(Guid WorkerId, string Name, bool Enabled, string CredentialStatus,
    DateTimeOffset? CredentialExpiresAtUtc, DateTimeOffset? LastHeartbeatUtc, bool Ready, IReadOnlyList<string> Languages,
    string? SandboxBackend, int ActiveJobs, int TotalCapacity, int UsedCapacity, string? LastFailureCode);
public interface IWorkerInventoryClient { Task<PagedResponse<WorkerInventoryItem>?> ListAsync(string? search, string? readiness, CancellationToken cancellationToken); }
public sealed class WorkerInventoryClient(IApiTransport transport) : IWorkerInventoryClient
{
    public Task<PagedResponse<WorkerInventoryItem>?> ListAsync(string? search, string? readiness, CancellationToken cancellationToken) =>
        transport.GetAsync<PagedResponse<WorkerInventoryItem>>($"/api/queries/workers?search={Uri.EscapeDataString(search ?? "")}&readiness={Uri.EscapeDataString(readiness ?? "")}&offset=0&limit=100", cancellationToken);
}
