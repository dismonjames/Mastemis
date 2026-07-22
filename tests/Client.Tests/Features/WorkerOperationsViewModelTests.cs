using Mastemis.Client.Core.Features.Workers;
using Mastemis.Client.Core.Networking.Http;

namespace Mastemis.Client.Tests.Features;

public sealed class WorkerOperationsViewModelTests
{
    [Fact]
    public async Task Refresh_maps_shared_capacity_without_exposing_credentials()
    {
        var viewModel = new WorkerOperationsViewModel(new Stub());
        await viewModel.RefreshAsync(CancellationToken.None);
        Assert.Single(viewModel.Workers);
        Assert.Equal(4, viewModel.TotalCapacity);
        Assert.Equal(2, viewModel.UsedCapacity);
    }

    private sealed class Stub : IWorkerInventoryClient
    {
        public Task<PagedResponse<WorkerInventoryItem>?> ListAsync(string? search, string? readiness, CancellationToken cancellationToken) =>
            Task.FromResult<PagedResponse<WorkerInventoryItem>?>(new([
                new(Guid.NewGuid(), "judge-1", true, "Active", null, DateTimeOffset.UtcNow, true,
                    ["cpp", "csharp"], "podman", 2, 4, 2, null)], 1, 50, 1));
    }
}
