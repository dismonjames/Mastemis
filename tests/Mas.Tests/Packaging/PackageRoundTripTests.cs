using Mastemis.Mas.Packaging.Archives;
using Mastemis.Mas.Packaging.Exporting;
using Mastemis.Mas.Packaging.Importing;
using Mastemis.Mas.Packaging.Manifest;
using Mastemis.Mas.Packaging.Security;
using Mastemis.Mas.Packaging.Validation;

namespace Mastemis.Mas.Tests.Packaging;

public sealed class PackageRoundTripTests
{
    [Fact]
    public async Task Export_is_deterministic_and_round_trip_restores_content()
    {
        var input = new Dictionary<string, byte[]>
        {
            ["statement/en.md"] = "# Sum"u8.ToArray(),
            ["tests/sample/1.in"] = "1 2\n"u8.ToArray(),
            ["tests/sample/1.out"] = "3\n"u8.ToArray(),
            ["generators/main.mas"] = "test 1 { input = int(1, 2) }"u8.ToArray()
        };
        var manifest = new ProblemPackageManifest("1.0", Guid.NewGuid(), "Sum", ["A"], ["math"], "easy", "en",
            new Dictionary<string, string> { ["en"] = "statement/en.md" }, new(1000, 64 * 1024 * 1024, 1024), ["cpp"],
            new("exact"), [new("sample", "sample", 0, 0)], [new("sample-1", "sample", 1, "tests/sample/1.in", "tests/sample/1.out", 4, 2)],
            [new("mas", "mas", "generators/main.mas")], [], [], new Dictionary<string, string>());
        var exporter = new ProblemPackageExporter(); await using var first = new MemoryStream(); await using var second = new MemoryStream();
        var a = await exporter.ExportAsync(manifest, input, first, TestContext.Current.CancellationToken);
        var b = await exporter.ExportAsync(manifest, input, second, TestContext.Current.CancellationToken);
        Assert.Equal(a.Sha256, b.Sha256); first.Position = 0;
        var importer = new ProblemPackageImporter(new PackageArchiveReader(new()), new ProblemPackageValidator());
        var restored = await importer.InspectAsync(first, TestContext.Current.CancellationToken);
        Assert.Equal(input["tests/sample/1.in"], restored.Package.Entries["tests/sample/1.in"]);
        Assert.Equal(manifest.ProblemId, restored.Package.Manifest.ProblemId);
    }
}
