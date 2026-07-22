namespace Mastemis.Judge.Languages.Cpp;

public sealed record CppLanguageOptions(string CompilerPath = "/usr/bin/g++", string Standard = "c++23")
{
    public void Validate()
    {
        if (!Path.IsPathFullyQualified(CompilerPath) || Standard is not ("c++20" or "c++23"))
            throw new ArgumentException("C++ toolchain configuration is invalid.");
    }
}
