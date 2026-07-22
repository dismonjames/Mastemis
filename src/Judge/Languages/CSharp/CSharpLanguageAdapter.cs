using System.Diagnostics;
using Mastemis.Contracts.Judge;
using Mastemis.Judge.Execution;

namespace Mastemis.Judge.Languages.CSharp;

public sealed class CSharpLanguageAdapter(ICompilerProcessRunner runner, CSharpLanguageOptions options) : ILanguageAdapter
{
    private static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.Ordinal) { ".cs" };
    public string LanguageId => "csharp";
    public IReadOnlySet<string> SourceExtensions => Extensions;

    public async ValueTask<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken cancellationToken)
    {
        options.Validate(); ValidatePaths(request); var stopwatch = Stopwatch.StartNew();
        var project = await CSharpProjectWriter.WriteAsync(request.BuildDirectory, request.SourcePaths, options.TargetFramework, cancellationToken);
        var packages = Path.Combine(request.BuildDirectory, "packages");
        var restoreArgs = new[] { "restore", project, "--configfile", Path.Combine(request.BuildDirectory, "NuGet.Config"),
            "--packages", packages, "--ignore-failed-sources", "--nologo", "--verbosity", "quiet" };
        var restore = await runner.RunAsync(options.DotnetPath, restoreArgs, request.BuildDirectory,
            request.Limits.CompilationTime, request.Limits.CompilationOutputBytes, cancellationToken);
        var restoreFailure = MapFailure(restore, stopwatch.Elapsed);
        if (restoreFailure is not null) return restoreFailure;

        var remainingTime = request.Limits.CompilationTime - stopwatch.Elapsed;
        var remainingOutput = request.Limits.CompilationOutputBytes - restore.OutputBytes;
        if (remainingTime <= TimeSpan.Zero) return Failed(JudgeFailureCode.CompilationTimedOut, restore, stopwatch.Elapsed);
        if (remainingOutput <= 0) return Failed(JudgeFailureCode.CompilationOutputLimit, restore, stopwatch.Elapsed);
        var output = Path.Combine(request.BuildDirectory, "out");
        var buildArgs = new[] { "build", project, "--configuration", "Release", "--no-restore", "--nologo",
            "--verbosity", "minimal", "--output", output };
        var build = await runner.RunAsync(options.DotnetPath, buildArgs, request.BuildDirectory, remainingTime, remainingOutput, cancellationToken);
        var failure = MapFailure(build, stopwatch.Elapsed, restore.OutputBytes);
        if (failure is not null) return failure;
        var artifact = Path.Combine(output, "program.dll");
        if (!File.Exists(artifact)) return Failed(JudgeFailureCode.ArtifactMissing, build, stopwatch.Elapsed, restore.OutputBytes);
        return new(true, new(artifact, Array.Empty<string>(), "dotnet"), Diagnostics(build), null,
            stopwatch.Elapsed, restore.OutputBytes + build.OutputBytes);
    }

    public ValueTask<ExecutionPlan> CreateExecutionPlanAsync(CompiledArtifact artifact, RuntimeEnvironment environment,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ExecutionPlan(options.DotnetPath,
            new[] { "/workspace/build/out/program.dll" }, environment.Variables));
    }

    private static void ValidatePaths(CompilationRequest request)
    {
        if (request.SourcePaths.Count is < 1 or > 32) throw new JudgeContractException(JudgeFailureCode.InvalidContract, "Invalid C# source count.");
        var root = Path.GetFullPath(request.SourceDirectory) + Path.DirectorySeparatorChar;
        if (request.SourcePaths.Any(path => !Path.GetFullPath(path).StartsWith(root, StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase)))
            throw new JudgeContractException(JudgeFailureCode.UnsafeSourceName, "C# source path escaped its workspace.");
    }
    private static CompilationResult? MapFailure(CompilerProcessResult result, TimeSpan duration, long priorBytes = 0)
    {
        if (result.TimedOut) return Failed(JudgeFailureCode.CompilationTimedOut, result, duration, priorBytes);
        if (result.OutputLimitExceeded) return Failed(JudgeFailureCode.CompilationOutputLimit, result, duration, priorBytes);
        if (result.ExitCode is null) return Failed(JudgeFailureCode.CompilerNotFound, result, duration, priorBytes);
        return result.ExitCode != 0 ? Failed(JudgeFailureCode.CompilationFailed, result, duration, priorBytes) : null;
    }
    private static CompilationResult Failed(JudgeFailureCode code, CompilerProcessResult result, TimeSpan duration, long priorBytes = 0) =>
        new(false, null, Diagnostics(result), code, duration, priorBytes + result.OutputBytes);
    private static IReadOnlyList<JudgeDiagnostic> Diagnostics(CompilerProcessResult result) => string.IsNullOrWhiteSpace(result.DiagnosticText)
        ? Array.Empty<JudgeDiagnostic>() : new[] { new JudgeDiagnostic("csharp.compiler", result.DiagnosticText, JudgeDiagnosticSeverity.Error) };
}
