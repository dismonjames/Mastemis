using System.Security;

namespace Mastemis.Judge.Languages.CSharp;

public static class CSharpProjectWriter
{
    public static async Task<string> WriteAsync(string buildDirectory, IReadOnlyList<string> sources,
        string targetFramework, CancellationToken cancellationToken)
    {
        var projectPath = Path.Combine(buildDirectory, "candidate.csproj");
        var compileItems = string.Join(Environment.NewLine, sources.Select(path =>
            $"    <Compile Include=\"{SecurityElement.Escape(Path.GetRelativePath(buildDirectory, path))}\" />"));
        var project = $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>{{targetFramework}}</TargetFramework>
                <AssemblyName>program</AssemblyName>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <Deterministic>true</Deterministic>
                <UseAppHost>false</UseAppHost>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <RunAnalyzers>false</RunAnalyzers>
                <NuGetAudit>false</NuGetAudit>
              </PropertyGroup>
              <ItemGroup>
            {{compileItems}}
              </ItemGroup>
            </Project>
            """;
        await File.WriteAllTextAsync(projectPath, project, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(buildDirectory, "NuGet.Config"),
            "<configuration><packageSources><clear /></packageSources></configuration>", cancellationToken);
        return projectPath;
    }
}
