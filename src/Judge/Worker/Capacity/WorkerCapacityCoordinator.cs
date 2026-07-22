namespace Mastemis.Judge.Worker.Capacity;

public enum WorkerJobCategory
{
    Submission,
    ReferenceOutput
}

public sealed class WorkerCapacityCoordinator
{
    private readonly SemaphoreSlim slots;
    private int activeSubmissions;
    private int activeReferenceOutputs;

    public WorkerCapacityCoordinator(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        slots = new(capacity, capacity);
    }

    public int Capacity { get; }
    public int ActiveSubmissions => Volatile.Read(ref activeSubmissions);
    public int ActiveReferenceOutputs => Volatile.Read(ref activeReferenceOutputs);
    public int Active => ActiveSubmissions + ActiveReferenceOutputs;

    public WorkerCapacityLease? TryAcquireSubmission()
    {
        if (!slots.Wait(0))
            return null;
        Interlocked.Increment(ref activeSubmissions);
        return new(this, WorkerJobCategory.Submission);
    }

    public async ValueTask<WorkerCapacityLease> AcquireReferenceOutputAsync(CancellationToken cancellationToken)
    {
        await slots.WaitAsync(cancellationToken);
        Interlocked.Increment(ref activeReferenceOutputs);
        return new(this, WorkerJobCategory.ReferenceOutput);
    }

    internal void Release(WorkerJobCategory category)
    {
        if (category == WorkerJobCategory.Submission)
            Interlocked.Decrement(ref activeSubmissions);
        else
            Interlocked.Decrement(ref activeReferenceOutputs);
        slots.Release();
    }
}

public sealed class WorkerCapacityLease : IDisposable
{
    private WorkerCapacityCoordinator? owner;

    internal WorkerCapacityLease(WorkerCapacityCoordinator owner, WorkerJobCategory category)
    {
        this.owner = owner;
        Category = category;
    }

    public WorkerJobCategory Category { get; }

    public void Dispose() => Interlocked.Exchange(ref owner, null)?.Release(Category);
}
