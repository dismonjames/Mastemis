using Mastemis.Contracts.Judge;
using Mastemis.Judge.Execution;
using Mastemis.Judge.Languages.CSharp;

namespace Mastemis.Judge.Tests.Languages;

public sealed class CSharpLanguageAdapterTests
{
    [Fact]
    public async Task Generated_project_has_no_packages_analyzers_or_candidate_properties()
    {
        await using var fixture = await CppLanguageAdapterTests.CompilationFixture.CreateAsync(".cs", "Console.WriteLine(1);");
        var project = await CSharpProjectWriter.WriteAsync(fixture.Request.BuildDirectory, fixture.Request.SourcePaths,
            "net10.0", TestContext.Current.CancellationToken);
        var text = await File.ReadAllTextAsync(project, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("PackageReference", text, StringComparison.Ordinal);
        Assert.Contains("<RunAnalyzers>false</RunAnalyzers>", text, StringComparison.Ordinal);
        Assert.Contains("<clear />", await File.ReadAllTextAsync(Path.Combine(fixture.Request.BuildDirectory, "NuGet.Config"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compiles_top_level_csharp_fixture_when_sdk_is_available()
    {
        if (!File.Exists("/usr/bin/dotnet")) Assert.Skip("The configured .NET SDK /usr/bin/dotnet is unavailable.");
        await using var fixture = await CppLanguageAdapterTests.CompilationFixture.CreateAsync(".cs", "Console.WriteLine(42);");
        var request = fixture.Request with { Limits = fixture.Request.Limits with { CompilationTime = TimeSpan.FromMinutes(1) } };
        var result = await new CSharpLanguageAdapter(new CompilerProcessRunner(), new()).CompileAsync(request, TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics.Select(x => x.Message)));
        Assert.NotNull(result.Artifact); Assert.True(File.Exists(result.Artifact.ExecutablePath));
    }

    [Fact]
    public async Task Maps_dotnet_compilation_failure()
    {
        await using var fixture = await CppLanguageAdapterTests.CompilationFixture.CreateAsync(".cs");
        var adapter = new CSharpLanguageAdapter(new CppLanguageAdapterTests.FakeRunner(new(1, false, false, TimeSpan.Zero, 2, "error")), new());
        var result = await adapter.CompileAsync(fixture.Request, TestContext.Current.CancellationToken);
        Assert.Equal(JudgeFailureCode.CompilationFailed, result.FailureCode);
    }
}
