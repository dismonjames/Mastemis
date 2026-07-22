using System.IO.Compression;
using Mastemis.Mas.Packaging.Archives;
using Mastemis.Mas.Packaging.Security;
using Mastemis.Mas.Packaging.Validation;

namespace Mastemis.Mas.Tests.Packaging;

public sealed class PackageArchiveSecurityTests
{
    [Theory]
    [InlineData("../escape")]
    [InlineData("/absolute")]
    [InlineData("C:/windows")]
    [InlineData("folder\\file")]
    public async Task Rejects_unsafe_paths(string path)
    {
        await using var package = Zip(("manifest.json", "{}"), (path, "x"));
        var error = await Assert.ThrowsAsync<PackageValidationException>(() => Reader().ReadAsync(package, TestContext.Current.CancellationToken));
        Assert.Equal("package.path.invalid", error.Diagnostics[0].Code);
    }

    [Fact]
    public async Task Rejects_case_and_unicode_normalization_collisions()
    {
        await using var package = Zip(("manifest.json", "{}"), ("assets/Café.txt", "a"), ("assets/Café.txt", "b"));
        var error = await Assert.ThrowsAsync<PackageValidationException>(() => Reader().ReadAsync(package, TestContext.Current.CancellationToken));
        Assert.Equal("package.path.duplicate", error.Diagnostics[0].Code);
    }

    [Fact]
    public async Task Rejects_missing_manifest_and_entry_limit()
    {
        await using var missing = Zip(("a.txt", "a"));
        await Assert.ThrowsAsync<PackageValidationException>(() => Reader().ReadAsync(missing, TestContext.Current.CancellationToken));
        await using var excessive = Zip(("manifest.json", "{}"), ("a", "a"));
        await Assert.ThrowsAsync<PackageValidationException>(() => new PackageArchiveReader(new(MaximumEntries: 1))
            .ReadAsync(excessive, TestContext.Current.CancellationToken));
    }

    private static PackageArchiveReader Reader() => new(new());
    internal static MemoryStream Zip(params (string Path, string Content)[] values)
    {
        var stream = new MemoryStream(); using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var value in values) { var entry = zip.CreateEntry(value.Path); using var writer = new StreamWriter(entry.Open()); writer.Write(value.Content); }
        stream.Position = 0; return stream;
    }
}
