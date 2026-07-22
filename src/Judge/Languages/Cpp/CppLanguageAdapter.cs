using Mastemis.Contracts.Judge;
using Mastemis.Judge.Execution;

namespace Mastemis.Judge.Languages.Cpp;

public sealed class CppLanguageAdapter(ICompilerProcessRunner runner, CppLanguageOptions options) : ILanguageAdapter
{
    private static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.Ordinal) { ".cpp", ".cc", ".cxx" };
    public string LanguageId => "cpp";
    public IReadOnlySet<string> SourceExtensions => Extensions;

    public async ValueTask<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken cancellationToken)
    {
        options.Validate(); ValidatePaths(request); var output = Path.Combine(request.BuildDirectory, "program");
        var arguments = BuildArguments(request.SourcePaths, output);
        var result = await runner.RunAsync(options.CompilerPath, arguments, request.BuildDirectory,
            request.Limits.CompilationTime, request.Limits.CompilationOutputBytes, cancellationToken);
        var diagnostic = string.IsNullOrWhiteSpace(result.DiagnosticText) ? Array.Empty<JudgeDiagnostic>() :
            new[] { new JudgeDiagnostic("cpp.compiler", result.DiagnosticText, JudgeDiagnosticSeverity.Error) };
        if (result.TimedOut) return Failed(JudgeFailureCode.CompilationTimedOut, result, diagnostic);
        if (result.OutputLimitExceeded) return Failed(JudgeFailureCode.CompilationOutputLimit, result, diagnostic);
        if (result.ExitCode is null) return Failed(JudgeFailureCode.CompilerNotFound, result, diagnostic);
        if (result.ExitCode != 0) return Failed(JudgeFailureCode.CompilationFailed, result, diagnostic);
        if (!File.Exists(output)) return Failed(JudgeFailureCode.ArtifactMissing, result, diagnostic);
        return new(true, new(output, Array.Empty<string>(), "native"), diagnostic, null, result.Duration, result.OutputBytes);
    }

    public ValueTask<ExecutionPlan> CreateExecutionPlanAsync(CompiledArtifact artifact, RuntimeEnvironment environment,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ExecutionPlan("/workspace/build/program", artifact.Arguments, environment.Variables));
    }

    public IReadOnlyList<string> BuildArguments(IReadOnlyList<string> sources, string output)
    {
        var arguments = new List<string> { $"-std={options.Standard}", "-O2", "-pipe", "-fno-diagnostics-color" };
        arguments.AddRange(sources); arguments.Add("-o"); arguments.Add(output); return arguments;
    }
    private static void ValidatePaths(CompilationRequest request)
    {
        if (request.SourcePaths.Count is < 1 or > 32) throw new JudgeContractException(JudgeFailureCode.InvalidContract, "Invalid C++ source count.");
        var sourceRoot = Path.GetFullPath(request.SourceDirectory) + Path.DirectorySeparatorChar;
        if (request.SourcePaths.Any(path => !Path.GetFullPath(path).StartsWith(sourceRoot, StringComparison.Ordinal) || !Extensions.Contains(Path.GetExtension(path).ToLowerInvariant())))
            throw new JudgeContractException(JudgeFailureCode.UnsafeSourceName, "C++ source path escaped its workspace.");
    }
    private static CompilationResult Failed(JudgeFailureCode code, CompilerProcessResult result, IReadOnlyList<JudgeDiagnostic> diagnostics) =>
        new(false, null, diagnostics, code, result.Duration, result.OutputBytes);
}
