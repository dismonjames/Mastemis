using Mastemis.Judge.Worker.Capacity;

namespace Mastemis.Judge.Tests.Worker.Capacity;

public sealed class WorkerCapacityCoordinatorTests
{
    [Fact]
    public async Task Submission_and_reference_jobs_share_one_strict_budget()
    {
        var coordinator = new WorkerCapacityCoordinator(2);
        using var first = coordinator.TryAcquireSubmission();
        using var second = coordinator.TryAcquireSubmission();
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Null(coordinator.TryAcquireSubmission());

        var waiting = coordinator.AcquireReferenceOutputAsync(TestContext.Current.CancellationToken).AsTask();
        Assert.False(waiting.IsCompleted);
        first!.Dispose();
        using var reference = await waiting.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal(2, coordinator.Active);
        Assert.Equal(1, coordinator.ActiveSubmissions);
        Assert.Equal(1, coordinator.ActiveReferenceOutputs);
    }

    [Fact]
    public void Capacity_is_released_exactly_once()
    {
        var coordinator = new WorkerCapacityCoordinator(1);
        var lease = coordinator.TryAcquireSubmission();
        Assert.NotNull(lease);
        lease!.Dispose();
        lease.Dispose();

        Assert.Equal(0, coordinator.Active);
        using var next = coordinator.TryAcquireSubmission();
        Assert.NotNull(next);
    }

    [Fact]
    public async Task Waiting_reference_acquisition_observes_cancellation()
    {
        var coordinator = new WorkerCapacityCoordinator(1);
        using var lease = coordinator.TryAcquireSubmission();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await coordinator.AcquireReferenceOutputAsync(cancellation.Token));
        Assert.Equal(1, coordinator.Active);
    }
}
