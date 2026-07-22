using System.IO.Compression;
using System.Text.Json;
using Mastemis.Mas.Packaging.Manifest;
using Mastemis.Mas.Packaging.Security;
using Mastemis.Mas.Packaging.Validation;

namespace Mastemis.Mas.Packaging.Archives;

public sealed record ProblemPackageDocument(ProblemPackageManifest Manifest, IReadOnlyDictionary<string, byte[]> Entries,
    string PackageSha256);

public sealed class PackageArchiveReader(PackageArchiveLimits limits)
{
    public async Task<ProblemPackageDocument> ReadAsync(Stream source, CancellationToken cancellationToken)
    {
        await using var bounded = new MemoryStream();
        await CopyBoundedAsync(source, bounded, limits.MaximumCompressedBytes, cancellationToken);
        var packageBytes = bounded.ToArray(); var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var caseNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
        try
        {
            using var archive = new ZipArchive(new MemoryStream(packageBytes, false), ZipArchiveMode.Read, false);
            if (archive.Entries.Count is 0 || archive.Entries.Count > limits.MaximumEntries) Fail("package.entries.limit", "Archive entry count is invalid.");
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.FullName.EndsWith('/')) continue;
                var path = PackagePath.Normalize(entry.FullName, limits.MaximumPathLength);
                if (!caseNames.Add(path) || entries.ContainsKey(path)) Fail("package.path.duplicate", "Archive contains a duplicate or case-colliding path.", path);
                if ((entry.ExternalAttributes >> 16 & 0xF000) == 0xA000) Fail("package.entry.symlink", "Symbolic-link entries are forbidden.", path);
                if (entry.Length < 0 || entry.Length > limits.MaximumEntryBytes || (path == "manifest.json" && entry.Length > limits.MaximumManifestBytes))
                    Fail("package.entry.limit", "Archive entry exceeds its size limit.", path);
                if (entry.CompressedLength > 0 && entry.Length / (double)entry.CompressedLength > limits.MaximumCompressionRatio)
                    Fail("package.ratio.limit", "Archive entry compression ratio is unsafe.", path);
                total = checked(total + entry.Length);
                if (total > limits.MaximumDecompressedBytes) Fail("package.total.limit", "Archive decompressed size exceeds its limit.");
                await using var input = entry.Open(); using var output = new MemoryStream((int)entry.Length);
                await CopyBoundedAsync(input, output, limits.MaximumEntryBytes, cancellationToken); entries[path] = output.ToArray();
            }
        }
        catch (PackageValidationException) { throw; }
        catch (Exception exception) when (exception is InvalidDataException or IOException or OverflowException)
        { Fail("package.archive.malformed", "Archive structure is malformed."); }
        if (!entries.TryGetValue("manifest.json", out var manifestBytes)) Fail("package.manifest.missing", "Manifest is missing.");
        ProblemPackageManifest manifest;
        try { manifest = JsonSerializer.Deserialize<ProblemPackageManifest>(manifestBytes, PackageJson.Options) ?? throw new JsonException(); }
        catch (JsonException) { Fail("package.manifest.json", "Manifest JSON is malformed."); throw; }
        return new(manifest, entries, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(packageBytes)).ToLowerInvariant());
    }

    private static async Task CopyBoundedAsync(Stream input, Stream output, long limit, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920]; long total = 0;
        while (true)
        {
            var count = await input.ReadAsync(buffer, cancellationToken); if (count == 0) return;
            total += count; if (total > limit) Fail("package.stream.limit", "Package stream exceeds its limit.");
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
    }
    private static void Fail(string code, string message, string? path = null) => throw new PackageValidationException([
        new(code, PackageDiagnosticSeverity.Error, message, path)]);
}
