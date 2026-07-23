namespace Mastemis.Client.Core.Features.ProblemStudio.Packages;

internal sealed class ProgressReadStream(Stream inner, Action<long> report) : Stream
{
    private long transferred;
    public override bool CanRead => inner.CanRead; public override bool CanSeek => inner.CanSeek; public override bool CanWrite => false;
    public override long Length => inner.Length; public override long Position { get => inner.Position; set => inner.Position = value; }
    public override void Flush() => inner.Flush(); public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override int Read(byte[] buffer, int offset, int count) { var read = inner.Read(buffer, offset, count); Report(read); return read; }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) { var read = await inner.ReadAsync(buffer, ct).ConfigureAwait(false); Report(read); return read; }
    protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    public override async ValueTask DisposeAsync() { await inner.DisposeAsync().ConfigureAwait(false); GC.SuppressFinalize(this); }
    private void Report(int read) { if (read <= 0) return; transferred += read; report(transferred); }
}
