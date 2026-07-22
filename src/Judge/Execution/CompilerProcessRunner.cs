using System.Diagnostics;
using System.Text;

namespace Mastemis.Judge.Execution;

public sealed class CompilerProcessRunner : ICompilerProcessRunner
{
    public async ValueTask<CompilerProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments,
        string workingDirectory, TimeSpan timeout, long outputLimit, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(executable)) throw new ArgumentException("Compiler path must be absolute.", nameof(executable));
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.Environment.Clear(); start.Environment["PATH"] = "/usr/bin:/bin"; start.Environment["LANG"] = "C.UTF-8";
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        var stopwatch = Stopwatch.StartNew();
        try { if (!process.Start()) return new(null, false, false, stopwatch.Elapsed, 0, "Compiler process did not start."); }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        { return new(null, false, false, stopwatch.Elapsed, 0, "Configured compiler executable was not found."); }

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        var output = new BoundedTextCapture(outputLimit);
        var stdout = CaptureAsync(process.StandardOutput, output, linked.Token);
        var stderr = CaptureAsync(process.StandardError, output, linked.Token);
        var timedOut = false;
        try { await process.WaitForExitAsync(linked.Token); await Task.WhenAll(stdout, stderr); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { timedOut = true; TryKill(process); }
        catch (OutputLimitException) { TryKill(process); }
        catch (OperationCanceledException) { TryKill(process); throw; }
        finally { if (!process.HasExited) TryKill(process); }
        return new(process.HasExited ? process.ExitCode : null, timedOut, output.Exceeded, stopwatch.Elapsed,
            output.TotalBytes, output.Text);
    }

    private static async Task CaptureAsync(StreamReader reader, BoundedTextCapture capture, CancellationToken cancellationToken)
    {
        var buffer = new char[2048];
        while (true)
        {
            var count = await reader.ReadAsync(buffer, cancellationToken); if (count == 0) return;
            capture.Append(buffer.AsSpan(0, count));
        }
    }
    private static void TryKill(Process process) { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }

    private sealed class BoundedTextCapture(long limit)
    {
        private readonly StringBuilder _text = new(); private readonly object _gate = new();
        public long TotalBytes { get; private set; }
        public bool Exceeded { get; private set; }
        public string Text { get { lock (_gate) return _text.ToString(); } }
        public void Append(ReadOnlySpan<char> value)
        {
            lock (_gate)
            {
                var bytes = Encoding.UTF8.GetByteCount(value); TotalBytes += bytes;
                if (TotalBytes > limit) { Exceeded = true; throw new OutputLimitException(); }
                if (_text.Length < 16_384) _text.Append(value[..Math.Min(value.Length, 16_384 - _text.Length)]);
            }
        }
    }
    private sealed class OutputLimitException : Exception;
}
