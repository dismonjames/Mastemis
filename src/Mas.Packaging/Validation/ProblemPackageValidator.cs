using System.Text.RegularExpressions;
using Mastemis.Mas.Packaging.Archives;
using Mastemis.Mas.Packaging.Checksums;
using Mastemis.Mas.Packaging.Versions;

namespace Mastemis.Mas.Packaging.Validation;

public sealed class ProblemPackageValidator
{
    private static readonly HashSet<string> Languages = new(StringComparer.Ordinal) { "cpp", "csharp" };
    private static readonly HashSet<string> Checkers = new(StringComparer.Ordinal) { "exact", "tokens" };
    public IReadOnlyList<PackageDiagnostic> Validate(ProblemPackageDocument package)
    {
        var m = package.Manifest; var d = new List<PackageDiagnostic>();
        Add(!PackageFormat.IsSupported(m.FormatVersion), "package.version.unsupported", "Format version is unsupported.", "formatVersion");
        Add(m.RequiredFeatures is { Count: > 0 }, "package.feature.unsupported", "Package requires unsupported critical features.", "requiredFeatures");
        Add(m.ProblemId == Guid.Empty, "package.id.invalid", "Problem ID is required.", "problemId");
        Add(string.IsNullOrWhiteSpace(m.Title) || m.Title.Length > 300, "package.title.invalid", "Title is invalid.", "title");
        Add(m.Authors.Count > 32 || m.Tags.Count > 64, "package.metadata.limit", "Author or tag count exceeds policy.");
        Add(!m.Statements.ContainsKey(m.DefaultLocale), "package.locale.default", "Default locale has no statement.", "defaultLocale");
        Add(m.Limits.TimeMilliseconds is < 50 or > 600_000 || m.Limits.MemoryBytes is < 16_777_216 or > 17_179_869_184 ||
            m.Limits.OutputBytes is < 1 or > 67_108_864, "package.limits.invalid", "Judge limits exceed policy.", "limits");
        Add(m.Languages.Count == 0 || m.Languages.Any(x => !Languages.Contains(x)), "package.language.unsupported", "A language is unsupported.", "languages");
        Add(!Checkers.Contains(m.Checker.Type), "package.checker.unsupported", "Checker is unsupported.", "checker.type");
        foreach (var path in m.Statements.Values.Concat(m.Assets).Concat(m.Generators.Select(x => x.Path)).Concat(m.ReferenceSolutions.Select(x => x.Path)))
            Add(!package.Entries.ContainsKey(path), "package.reference.missing", "Referenced entry is missing.", path: path);
        var groupIds = m.Groups.Select(x => x.Id).ToArray(); Add(groupIds.Distinct(StringComparer.Ordinal).Count() != groupIds.Length,
            "package.group.duplicate", "Group identifiers must be unique.");
        Add(m.Groups.Any(x => x.Weight < 0 || x.Order < 0 || x.Visibility is not ("sample" or "public" or "hidden")),
            "package.group.invalid", "Test group metadata is invalid.");
        var indexes = new HashSet<(string, int)>();
        foreach (var test in m.Tests)
        {
            Add(!indexes.Add((test.GroupId, test.Index)), "package.test.duplicate", "Test index is duplicated.", path: test.InputPath);
            Add(!groupIds.Contains(test.GroupId, StringComparer.Ordinal), "package.test.group", "Test references an unknown group.", path: test.InputPath);
            Add(!package.Entries.ContainsKey(test.InputPath) || test.OutputPath is not null && !package.Entries.ContainsKey(test.OutputPath),
                "package.test.missing", "Test input or output is missing.", path: test.InputPath);
            Add(test.OutputPath is null && Checkers.Contains(m.Checker.Type), "package.test.output", "Expected output is required.", path: test.InputPath);
        }
        foreach (var checksum in m.Checksums)
            Add(!package.Entries.TryGetValue(checksum.Key, out var bytes) || !Regex.IsMatch(checksum.Value, "^[0-9a-f]{64}$") ||
                bytes is not null && PackageChecksums.Sha256(bytes) != checksum.Value, "package.checksum.mismatch", "Entry checksum does not match.", path: checksum.Key);
        var referenced = m.Statements.Values.Concat(m.Assets).Concat(m.Generators.Select(x => x.Path)).Concat(m.ReferenceSolutions.Select(x => x.Path))
            .Concat(m.Tests.SelectMany(x => x.OutputPath is null ? new[] { x.InputPath } : new[] { x.InputPath, x.OutputPath })).ToHashSet(StringComparer.Ordinal);
        foreach (var path in package.Entries.Keys.Where(x => x != "manifest.json" && x != "metadata/checksums.json" && IsCritical(x)))
            Add(!referenced.Contains(path), "package.entry.unreferenced", "Critical package entry is not referenced by the manifest.", path: path);
        return d;
        void Add(bool condition, string code, string message, string? property = null, string? path = null)
        { if (condition) d.Add(new(code, PackageDiagnosticSeverity.Error, message, path, property)); }
        static bool IsCritical(string path) => path.StartsWith("tests/", StringComparison.Ordinal) || path.StartsWith("solutions/", StringComparison.Ordinal) ||
            path.StartsWith("generators/", StringComparison.Ordinal) || path.StartsWith("checkers/", StringComparison.Ordinal) ||
            path.StartsWith("statement/", StringComparison.Ordinal) || path.StartsWith("assets/", StringComparison.Ordinal);
    }
}
