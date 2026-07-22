namespace Mastemis.Sandbox.Resources;

public sealed class SandboxOutputBudget(long maximumBytes)
{
    private long _consumed;

    public long ConsumedBytes => Interlocked.Read(ref _consumed);

    public bool TryConsume(int byteCount)
    {
        if (byteCount < 0) throw new ArgumentOutOfRangeException(nameof(byteCount));
        return Interlocked.Add(ref _consumed, byteCount) <= maximumBytes;
    }
}
