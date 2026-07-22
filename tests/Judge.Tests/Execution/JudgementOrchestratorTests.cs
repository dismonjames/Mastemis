using Mastemis.Contracts.Judge;
using Mastemis.Domain;
using Mastemis.Judge.Checking;
using Mastemis.Judge.Configuration;
using Mastemis.Judge.Execution;
using Mastemis.Judge.Languages;
using Mastemis.Judge.Workspaces;
using Mastemis.Sandbox.Abstractions;
using Mastemis.Sandbox.Contracts;

namespace Mastemis.Judge.Tests.Execution;

public sealed class JudgementOrchestratorTests
{
    [Theory]
    [InlineData(SandboxExitKind.TimedOut, null, null, SubmissionState.TimeLimitExceeded)]
    [InlineData(SandboxExitKind.ResourceLimit, SandboxResourceViolation.Memory, null, SubmissionState.MemoryLimitExceeded)]
    [InlineData(SandboxExitKind.ResourceLimit, SandboxResourceViolation.Output, null, SubmissionState.OutputLimitExceeded)]
    [InlineData(SandboxExitKind.Exited, null, 1, SubmissionState.RuntimeError)]
    [InlineData(SandboxExitKind.InfrastructureFailure, null, null, SubmissionState.InfrastructureError)]
    public async Task Maps_sandbox_outcomes(SandboxExitKind kind, SandboxResourceViolation? violation, int? exitCode, SubmissionState verdict)
    {
        await using var fixture = new Fixture(new FakeSandbox(new(kind, exitCode, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1),
            1024, 0, 0, violation, kind == SandboxExitKind.InfrastructureFailure ? "failure" : null, "fake")));
        Assert.Equal(verdict, (await fixture.ExecuteAsync("expected"u8.ToArray())).Verdict);
    }

    [Fact]
    public async Task Accepts_matching_output_and_rejects_mismatch_in_test_order()
    {
        await using var accepted = new Fixture(new FakeSandbox(Output("42\n")));
        Assert.Equal(SubmissionState.Accepted, (await accepted.ExecuteAsync("42\n"u8.ToArray())).Verdict);
        await using var wrong = new Fixture(new FakeSandbox(Output("41\n")));
        var result = await wrong.ExecuteAsync("42\n"u8.ToArray()); Assert.Equal(SubmissionState.WrongAnswer, result.Verdict); Assert.Equal(1, result.FailedTestIndex);
    }

    [Fact]
    public async Task Compilation_and_checker_failures_are_distinct_and_workspace_is_cleaned()
    {
        await using var fixture = new Fixture(new FakeSandbox(Output("42")), compile: new(false, null, [], JudgeFailureCode.CompilationFailed, TimeSpan.Zero, 1));
        Assert.Equal(SubmissionState.CompilationError, (await fixture.ExecuteAsync("42"u8.ToArray())).Verdict);
        Assert.Empty(Directory.EnumerateDirectories(fixture.Root));
    }

    [Fact]
    public async Task Cancellation_propagates_and_workspace_is_cleaned()
    {
        await using var fixture = new Fixture(new FakeSandbox(Output("42"))); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.ExecuteAsync("42"u8.ToArray(), cancellation.Token));
        Assert.False(Directory.Exists(fixture.Root) && Directory.EnumerateDirectories(fixture.Root).Any());
    }

    private static SandboxResult Output(string content) => new(SandboxExitKind.Exited, 0, null, TimeSpan.Zero,
        TimeSpan.FromMilliseconds(1), 1024, System.Text.Encoding.UTF8.GetByteCount(content), 0, null, content, "fake");

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly JudgementOrchestrator _orchestrator; private readonly FakeSandbox _sandbox;
        public Fixture(FakeSandbox sandbox, CompilationResult? compile = null)
        {
            _sandbox = sandbox; Root = Path.Combine(Path.GetTempPath(), $"mastemis-orchestrator-{Guid.NewGuid():N}");
            _orchestrator = new([new FakeLanguage(compile)], [new ExactOutputChecker(), new TokenOutputChecker()],
                new JudgeWorkspaceManager(Root), sandbox, new Clock(), new("image", TimeSpan.FromSeconds(10), 1024 * 1024, "test"));
        }
        public string Root { get; }
        public async Task<JudgeExecutionResult> ExecuteAsync(byte[] expected, CancellationToken? cancellationToken = null) => await _orchestrator.ExecuteAsync(new(
            JudgeJobId.New(), SubmissionId.New(), JudgeWorkerId.New(), "fake", [new("main.cs", "source"u8.ToArray())],
            [new(1, Array.Empty<byte>(), expected, "exact")], Contracts.JudgeContractTests.ValidLimits(),
            new("x64", new Dictionary<string, string>())), cancellationToken ?? TestContext.Current.CancellationToken);
        public ValueTask DisposeAsync() { if (Directory.Exists(Root)) Directory.Delete(Root, true); return ValueTask.CompletedTask; }
    }
    private sealed class FakeLanguage(CompilationResult? result) : ILanguageAdapter
    {
        public string LanguageId => "fake"; public IReadOnlySet<string> SourceExtensions { get; } = new HashSet<string> { ".cs" };
        public ValueTask<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken cancellationToken)
        { var artifact = Path.Combine(request.BuildDirectory, "program"); File.WriteAllText(artifact, "artifact"); return ValueTask.FromResult(result ?? new(true, new(artifact, [], "fake"), [], null, TimeSpan.Zero, 0)); }
        public ValueTask<ExecutionPlan> CreateExecutionPlanAsync(CompiledArtifact artifact, RuntimeEnvironment environment, CancellationToken cancellationToken) => ValueTask.FromResult(new ExecutionPlan("/workspace/build/program", [], environment.Variables));
    }
    private sealed class FakeSandbox(SandboxResult result) : ISandboxRunner
    {
        public async ValueTask<SandboxResult> RunAsync(SandboxRequest request, CancellationToken cancellationToken)
        { if (result.InfrastructureDiagnostic is { } output && result.ExitKind == SandboxExitKind.Exited) await File.WriteAllTextAsync(request.StandardOutputPath, output, cancellationToken); else if (result.ExitKind == SandboxExitKind.Exited) await File.WriteAllBytesAsync(request.StandardOutputPath, [], cancellationToken); await File.WriteAllBytesAsync(request.StandardErrorPath, [], cancellationToken); return result; }
    }
    private sealed class Clock : IJudgeClock { public DateTimeOffset UtcNow => new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero); }
}
