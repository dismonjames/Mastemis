using Mastemis.Contracts.Judge;

namespace Mastemis.Judge.Workspaces;

public static class SourceNameValidator
{
    public static void Validate(string name, IReadOnlySet<string> allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || Path.IsPathRooted(name) || name != Path.GetFileName(name) ||
            name.Contains('/') || name.Contains('\\') || name is "." or ".." || name.Any(character => character > 127 || char.IsControl(character)))
            throw Unsafe();
        if (name.StartsWith(".", StringComparison.Ordinal) || name.Contains("..", StringComparison.Ordinal) ||
            name.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.')))
            throw Unsafe();
        if (!allowedExtensions.Contains(Path.GetExtension(name).ToLowerInvariant())) throw Unsafe();
    }
    private static JudgeContractException Unsafe() => new(JudgeFailureCode.UnsafeSourceName, "Source filename is unsafe or unsupported.");
}
