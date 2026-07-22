namespace Mastemis.Judge.Execution;

public interface ICompilerProcessRunner
{
    ValueTask<CompilerProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments,
        string workingDirectory, TimeSpan timeout, long outputLimit, CancellationToken cancellationToken);
}

public sealed record CompilerProcessResult(int? ExitCode, bool TimedOut, bool OutputLimitExceeded,
    TimeSpan Duration, long OutputBytes, string DiagnosticText);
