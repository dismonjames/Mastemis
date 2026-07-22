namespace Mastemis.Judge.Languages.CSharp;

public sealed record CSharpLanguageOptions(string DotnetPath = "/usr/bin/dotnet", string TargetFramework = "net10.0")
{
    public void Validate()
    {
        if (!Path.IsPathFullyQualified(DotnetPath) || TargetFramework is not ("net9.0" or "net10.0"))
            throw new ArgumentException("C# toolchain configuration is invalid.");
    }
}
