using System.Text.Json.Serialization;

namespace Mastemis.Mas.Packaging.Manifest;

public sealed record ProblemPackageManifest(
    [property: JsonPropertyName("formatVersion")] string FormatVersion,
    [property: JsonPropertyName("problemId")] Guid ProblemId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("authors")] IReadOnlyList<string> Authors,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("difficulty")] string Difficulty,
    [property: JsonPropertyName("defaultLocale")] string DefaultLocale,
    [property: JsonPropertyName("statements")] IReadOnlyDictionary<string, string> Statements,
    [property: JsonPropertyName("limits")] JudgeLimitsManifest Limits,
    [property: JsonPropertyName("languages")] IReadOnlyList<string> Languages,
    [property: JsonPropertyName("checker")] CheckerManifest Checker,
    [property: JsonPropertyName("groups")] IReadOnlyList<TestGroupManifest> Groups,
    [property: JsonPropertyName("tests")] IReadOnlyList<TestFileManifest> Tests,
    [property: JsonPropertyName("generators")] IReadOnlyList<SourceEntryManifest> Generators,
    [property: JsonPropertyName("referenceSolutions")] IReadOnlyList<SourceEntryManifest> ReferenceSolutions,
    [property: JsonPropertyName("assets")] IReadOnlyList<string> Assets,
    [property: JsonPropertyName("checksums")] IReadOnlyDictionary<string, string> Checksums,
    [property: JsonPropertyName("createdBy")] string? CreatedBy = null,
    [property: JsonPropertyName("signature")] PackageSignatureManifest? Signature = null,
    [property: JsonPropertyName("requiredFeatures")] IReadOnlyList<string>? RequiredFeatures = null);

public sealed record JudgeLimitsManifest(long TimeMilliseconds, long MemoryBytes, long OutputBytes);
public sealed record CheckerManifest(string Type, IReadOnlyDictionary<string, string>? Configuration = null);
public sealed record TestGroupManifest(string Id, string Visibility, int Order, int Weight, bool FailFast = true,
    string Source = "static", string? CheckerOverride = null);
public sealed record TestFileManifest(string Id, string GroupId, int Index, string InputPath, string? OutputPath,
    long InputBytes, long? OutputBytes);
public sealed record SourceEntryManifest(string Id, string Language, string Path);
public sealed record PackageSignatureManifest(string Algorithm, string KeyId, string Value);
