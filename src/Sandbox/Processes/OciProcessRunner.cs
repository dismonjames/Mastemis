using System.Diagnostics;
using Mastemis.Sandbox.Contracts;
using Mastemis.Sandbox.Resources;

namespace Mastemis.Sandbox.Processes;

internal static class OciProcessRunner
{
    public static async Task<SandboxResult> RunAsync(string runtime, IReadOnlyList<string> arguments, SandboxRequest request,
        string backend, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(runtime)
        {
            UseShellExecute = false,
            RedirectStandardInput = request.StandardInputPath is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.Environment.Clear(); start.Environment["PATH"] = "/usr/bin:/bin"; start.Environment["LANG"] = "C.UTF-8";
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start }; var stopwatch = Stopwatch.StartNew();
        try { if (!process.Start()) return Failure("OCI runtime did not start.", backend, stopwatch.Elapsed); }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        { return Failure("Configured OCI runtime was not found.", backend, stopwatch.Elapsed); }
        using var timeout = new CancellationTokenSource(request.Limits.WallTime);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var outputBudget = new SandboxOutputBudget(request.Limits.OutputBytes);
        var stdout = CopyBoundedAsync(process.StandardOutput.BaseStream, request.StandardOutputPath, outputBudget, linked.Token);
        var stderr = CopyBoundedAsync(process.StandardError.BaseStream, request.StandardErrorPath, outputBudget, linked.Token);
        var input = request.StandardInputPath is null ? Task.CompletedTask : CopyInputAsync(request.StandardInputPath, process.StandardInput.BaseStream, linked.Token);
        try
        {
            var wait = process.WaitForExitAsync(linked.Token);
            var firstOutput = await Task.WhenAny(stdout, stderr);
            if (firstOutput.IsFaulted) await firstOutput;
            await wait; var sizes = await Task.WhenAll(stdout, stderr); await input;
            if (process.ExitCode is 125 or 126 or 127) return Failure("OCI runtime failed to launch the isolated command.", backend, stopwatch.Elapsed);
            return new(SandboxExitKind.Exited, process.ExitCode, null, TimeSpan.Zero, stopwatch.Elapsed, null,
                sizes[0], sizes[1], null, null, backend);
        }
        catch (SandboxOutputLimitException)
        {
            TryKill(process); return new(SandboxExitKind.ResourceLimit, null, null, TimeSpan.Zero,
            stopwatch.Elapsed, null, request.Limits.OutputBytes, 0, SandboxResourceViolation.Output, null, backend);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { TryKill(process); return new(SandboxExitKind.TimedOut, null, null, TimeSpan.Zero, stopwatch.Elapsed, null, 0, 0, SandboxResourceViolation.WallTime, null, backend); }
        catch (OperationCanceledException) { TryKill(process); return new(SandboxExitKind.Cancelled, null, null, TimeSpan.Zero, stopwatch.Elapsed, null, 0, 0, null, null, backend); }
    }
    private static async Task<long> CopyBoundedAsync(Stream input, string path, SandboxOutputBudget budget, CancellationToken cancellationToken)
    {
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 81920, FileOptions.Asynchronous);
        var buffer = new byte[81920]; long total = 0;
        while (true)
        {
            var count = await input.ReadAsync(buffer, cancellationToken); if (count == 0) return total;
            total += count; if (!budget.TryConsume(count)) throw new SandboxOutputLimitException(); await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
    }
    private static async Task CopyInputAsync(string path, Stream output, CancellationToken cancellationToken)
    { await using var input = File.OpenRead(path); await input.CopyToAsync(output, cancellationToken); await output.DisposeAsync(); }
    private static void TryKill(Process process) { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }
    private static SandboxResult Failure(string diagnostic, string backend, TimeSpan duration) => new(SandboxExitKind.InfrastructureFailure,
        null, null, TimeSpan.Zero, duration, null, 0, 0, null, diagnostic, backend);
    private sealed class SandboxOutputLimitException : Exception;
}
