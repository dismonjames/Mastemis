using Mastemis.Contracts.Judge;
using Mastemis.Judge.Execution;
using Mastemis.Judge.Languages.Cpp;

namespace Mastemis.Judge.Tests.Languages;

public sealed class CppLanguageAdapterTests
{
    [Fact]
    public void Arguments_are_structured_and_candidate_flags_cannot_be_injected()
    {
        var adapter = new CppLanguageAdapter(new FakeRunner(new(0, false, false, TimeSpan.Zero, 0, "")), new());
        var args = adapter.BuildArguments(["/workspace/source/source_001.cpp"], "/workspace/build/program");
        Assert.Equal("-std=c++23", args[0]); Assert.Contains("/workspace/source/source_001.cpp", args);
        Assert.DoesNotContain(args, argument => argument.Contains(';', StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1, JudgeFailureCode.CompilationFailed)]
    [InlineData(null, JudgeFailureCode.CompilerNotFound)]
    public async Task Maps_compiler_failures(int? exitCode, JudgeFailureCode expected)
    {
        await using var fixture = await CompilationFixture.CreateAsync(".cpp");
        var adapter = new CppLanguageAdapter(new FakeRunner(new(exitCode, false, false, TimeSpan.Zero, 10, "diagnostic")), new());
        var result = await adapter.CompileAsync(fixture.Request, TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded); Assert.Equal(expected, result.FailureCode);
    }

    [Fact]
    public async Task Compiles_valid_cpp23_fixture_when_toolchain_is_available()
    {
        if (!File.Exists("/usr/bin/g++")) Assert.Skip("The configured C++ compiler /usr/bin/g++ is unavailable.");
        await using var fixture = await CompilationFixture.CreateAsync(".cpp", "#include <iostream>\nint main(){std::cout << 42 << '\\n';}");
        var result = await new CppLanguageAdapter(new CompilerProcessRunner(), new()).CompileAsync(fixture.Request, TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded); Assert.NotNull(result.Artifact); Assert.True(File.Exists(result.Artifact.ExecutablePath));
    }

    internal sealed class FakeRunner(CompilerProcessResult result) : ICompilerProcessRunner
    {
        public ValueTask<CompilerProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, string workingDirectory,
            TimeSpan timeout, long outputLimit, CancellationToken cancellationToken) => ValueTask.FromResult(result);
    }
    internal sealed class CompilationFixture : IAsyncDisposable
    {
        private CompilationFixture(string root, CompilationRequest request) { Root = root; Request = request; }
        public string Root { get; }
        public CompilationRequest Request { get; }
        public static async Task<CompilationFixture> CreateAsync(string extension, string content = "source")
        {
            var root = Path.Combine(Path.GetTempPath(), $"mastemis-compile-{Guid.NewGuid():N}"); var source = Path.Combine(root, "source"); var build = Path.Combine(root, "build");
            Directory.CreateDirectory(source); Directory.CreateDirectory(build); var file = Path.Combine(source, $"source_001{extension}"); await File.WriteAllTextAsync(file, content, TestContext.Current.CancellationToken);
            return new(root, new([file], source, build, Contracts.JudgeContractTests.ValidLimits()));
        }
        public ValueTask DisposeAsync() { if (Directory.Exists(Root)) Directory.Delete(Root, true); return ValueTask.CompletedTask; }
    }
}
