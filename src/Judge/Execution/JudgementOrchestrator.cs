using Mastemis.Contracts.Judge;
using Mastemis.Domain;
using Mastemis.Judge.Checking;
using Mastemis.Judge.Configuration;
using Mastemis.Judge.Languages;
using Mastemis.Judge.Workspaces;
using Mastemis.Sandbox.Abstractions;
using Mastemis.Sandbox.Contracts;

namespace Mastemis.Judge.Execution;

public sealed class JudgementOrchestrator(
    IEnumerable<ILanguageAdapter> languages,
    IEnumerable<IOutputChecker> checkers,
    IJudgeWorkspaceManager workspaces,
    ISandboxRunner sandbox,
    IJudgeClock clock,
    JudgeOrchestratorOptions options) : IJudgementOrchestrator
{
    public async ValueTask<JudgeExecutionResult> ExecuteAsync(JudgeExecutionRequest request, CancellationToken cancellationToken)
    {
        var started = clock.UtcNow; options.Validate(); request.Validate();
        var language = languages.SingleOrDefault(x => string.Equals(x.LanguageId, request.LanguageId, StringComparison.OrdinalIgnoreCase))
            ?? throw new JudgeContractException(JudgeFailureCode.UnsupportedLanguage, "The requested language is not configured.");
        using var totalTimeout = new CancellationTokenSource(options.TotalTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, totalTimeout.Token);
        await using var workspace = await workspaces.CreateAsync(linked.Token);
        var sources = await workspace.MaterializeSourcesAsync(request.Sources, language.SourceExtensions, linked.Token);
        var compilation = await language.CompileAsync(new(sources.Select(x => x.InternalPath).ToArray(), workspace.SourceDirectory,
            workspace.BuildDirectory, request.Limits), linked.Token);
        if (!compilation.Succeeded) return CompilationFailure(request, started, compilation);
        var artifact = compilation.Artifact ?? throw new JudgeContractException(JudgeFailureCode.ArtifactMissing, "Compiler returned no artifact.");
        var plan = await language.CreateExecutionPlanAsync(artifact, request.Environment, linked.Token);
        var elapsed = TimeSpan.Zero; long? peakMemory = null; long stdoutBytes = 0; long stderrBytes = 0; var sandboxBackend = "not-started";
        foreach (var test in request.Tests.OrderBy(x => x.Index))
        {
            ValidateTestData(test); var input = Path.Combine(workspace.InputDirectory, $"test-{test.Index:D5}.in");
            var output = Path.Combine(workspace.OutputDirectory, $"test-{test.Index:D5}.out");
            var error = Path.Combine(workspace.OutputDirectory, $"test-{test.Index:D5}.err");
            await File.WriteAllBytesAsync(input, test.Input.ToArray(), linked.Token);
            var sandboxResult = await sandbox.RunAsync(new(options.Image, plan.Executable, plan.Arguments, workspace.Root,
                input, output, error, plan.Environment, new(request.Limits.CpuTime, request.Limits.WallTime,
                    request.Limits.MemoryBytes, request.Limits.OutputBytes, request.Limits.FileBytes, request.Limits.ProcessCount)), linked.Token);
            sandboxBackend = sandboxResult.Backend;
            elapsed += sandboxResult.WallTime; peakMemory = Math.Max(peakMemory ?? 0, sandboxResult.PeakMemoryBytes ?? 0);
            stdoutBytes += sandboxResult.StandardOutputBytes; stderrBytes += sandboxResult.StandardErrorBytes;
            var mapped = SandboxVerdictMapper.Map(sandboxResult);
            if (mapped.Verdict is { } verdict) return Result(request, verdict, started, elapsed, peakMemory, sandboxResult,
                stdoutBytes, stderrBytes, compilation.Diagnostics, sandboxResult.InfrastructureDiagnostic, test.Index);
            var checker = checkers.SingleOrDefault(x => string.Equals(x.CheckerId, test.CheckerId, StringComparison.OrdinalIgnoreCase));
            if (checker is null) return Result(request, SubmissionState.InfrastructureError, started, elapsed, peakMemory,
                sandboxResult, stdoutBytes, stderrBytes, compilation.Diagnostics, "Configured checker is unavailable.", test.Index);
            var actual = await File.ReadAllBytesAsync(output, linked.Token);
            var check = await checker.CheckAsync(new(test.ExpectedOutput, actual, request.Limits.OutputBytes), linked.Token);
            if (!check.Accepted) return Result(request, SubmissionState.WrongAnswer, started, elapsed, peakMemory,
                sandboxResult, stdoutBytes, stderrBytes, compilation.Diagnostics, check.Diagnostic?.Code, test.Index);
        }
        return new(SubmissionState.Accepted, elapsed, peakMemory, 0, null, stdoutBytes, stderrBytes,
            compilation.Diagnostics, null, null, request.WorkerId, options.JudgeVersion, sandboxBackend, started, clock.UtcNow);
    }

    private void ValidateTestData(TestCaseDescriptor test)
    {
        if (test.Input.Length > options.MaximumTestDataBytes || test.ExpectedOutput.Length > options.MaximumTestDataBytes ||
            string.IsNullOrWhiteSpace(test.CheckerId) || test.CheckerId.Length > 32)
            throw new JudgeContractException(JudgeFailureCode.InvalidContract, "Test data is invalid or too large.");
    }
    private JudgeExecutionResult CompilationFailure(JudgeExecutionRequest request, DateTimeOffset started, CompilationResult compilation)
    {
        var infrastructure = compilation.FailureCode is JudgeFailureCode.CompilerNotFound or JudgeFailureCode.ArtifactMissing;
        return new(infrastructure ? SubmissionState.InfrastructureError : SubmissionState.CompilationError, compilation.Duration, null,
            null, null, 0, compilation.OutputBytes, compilation.Diagnostics, compilation.FailureCode?.ToString(), null,
            request.WorkerId, options.JudgeVersion, "not-started", started, clock.UtcNow);
    }
    private JudgeExecutionResult Result(JudgeExecutionRequest request, SubmissionState verdict, DateTimeOffset started,
        TimeSpan elapsed, long? memory, SandboxResult sandboxResult, long stdout, long stderr,
        IReadOnlyList<JudgeDiagnostic> diagnostics, string? reason, int failedTest) => new(verdict, elapsed, memory,
        sandboxResult.ExitCode, sandboxResult.Signal, stdout, stderr, diagnostics, reason, failedTest, request.WorkerId,
        options.JudgeVersion, sandboxResult.Backend, started, clock.UtcNow);
}
