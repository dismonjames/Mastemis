using System.Text.Json;
using Mastemis.Mas.Packaging.Manifest;
using Mastemis.Mas.Packaging.Versions;

namespace Mastemis.Mas.Tests.Packaging;

public sealed class ManifestTests
{
    [Fact]
    public void Manifest_uses_stable_camel_case_schema()
    {
        var manifest = new ProblemPackageManifest(PackageFormat.CurrentVersion, Guid.NewGuid(), "Sum", ["Author"],
            ["math"], "easy", "en", new Dictionary<string, string> { ["en"] = "statement/en.md" },
            new(1000, 64 * 1024 * 1024, 1024), new[] { "cpp", "csharp" }, new("exact"), [], [], [], [],
            [], new Dictionary<string, string>());
        var json = JsonSerializer.Serialize(manifest, PackageJson.Options);
        Assert.Contains("\"formatVersion\": \"1.0\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("FormatVersion", json, StringComparison.Ordinal);
    }
}
