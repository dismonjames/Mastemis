using System.Buffers.Binary;
using Mastemis.Mas.Packaging.Archives;
using Mastemis.Mas.Packaging.Security;
using Mastemis.Mas.Packaging.Validation;

namespace Mastemis.Mas.Tests.Packaging;

public sealed class ZipFeatureSecurityTests
{
    [Theory]
    [InlineData(true, false, "package.archive.encrypted")]
    [InlineData(false, true, "package.archive.compression_method")]
    public async Task Rejects_encryption_and_unsupported_compression(bool encrypted, bool method, string code)
    {
        await using var valid = PackageArchiveSecurityTests.Zip(("manifest.json", "{}")); var bytes = valid.ToArray();
        PatchHeaders(bytes, encrypted ? (ushort)1 : (ushort)0, method ? (ushort)99 : (ushort)0);
        var error = await Assert.ThrowsAsync<PackageValidationException>(() => Reader().ReadAsync(new MemoryStream(bytes), TestContext.Current.CancellationToken));
        Assert.Equal(code, error.Diagnostics[0].Code);
    }

    [Fact]
    public async Task Rejects_trailing_data_and_nested_archives()
    {
        await using var valid = PackageArchiveSecurityTests.Zip(("manifest.json", "{}")); var trailing = valid.ToArray().Concat(new byte[] { 1 }).ToArray();
        var error = await Assert.ThrowsAsync<PackageValidationException>(() => Reader().ReadAsync(new MemoryStream(trailing), TestContext.Current.CancellationToken));
        Assert.Equal("package.archive.trailing_data", error.Diagnostics[0].Code);
        await using var nestedContent = PackageArchiveSecurityTests.Zip(("nested.txt", "x"));
        var nestedBytes = nestedContent.ToArray();
        await using var actualNested = ZipBytes(("manifest.json", "{}"u8.ToArray()), ("assets/nested.bin", nestedBytes));
        error = await Assert.ThrowsAsync<PackageValidationException>(() => Reader().ReadAsync(actualNested, TestContext.Current.CancellationToken));
        Assert.Equal("package.archive.nested", error.Diagnostics[0].Code);
    }
    private static PackageArchiveReader Reader() => new(new());

    private static MemoryStream ZipBytes(params (string Name, byte[] Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, true))
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var output = entry.Open();
                output.Write(content);
            }
        stream.Position = 0;
        return stream;
    }
    private static void PatchHeaders(byte[] bytes, ushort flags, ushort method)
    {
        for (var i = 0; i <= bytes.Length - 4; i++)
        {
            var signature = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i));
            if (signature == 0x04034b50) { BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i + 6), flags); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i + 8), method); }
            if (signature == 0x02014b50) { BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i + 8), flags); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i + 10), method); }
        }
    }
}
