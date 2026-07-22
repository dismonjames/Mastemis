using System.IO.Compression;
using System.Text.Json;
using Mastemis.Mas.Packaging.Checksums;
using Mastemis.Mas.Packaging.Manifest;

namespace Mastemis.Mas.Packaging.Exporting;

public sealed class ProblemPackageExporter
{
    private static readonly DateTimeOffset Timestamp = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public async Task<(string Sha256, long Length)> ExportAsync(ProblemPackageManifest manifest,
        IReadOnlyDictionary<string, byte[]> content, Stream destination, CancellationToken cancellationToken)
    {
        var checksums = content.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key,
            x => PackageChecksums.Sha256(x.Value), StringComparer.Ordinal);
        manifest = manifest with { Checksums = checksums };
        var entries = new SortedDictionary<string, byte[]>(content.ToDictionary(), StringComparer.Ordinal)
        {
            ["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(manifest, PackageJson.Options),
            ["metadata/checksums.json"] = JsonSerializer.SerializeToUtf8Bytes(checksums, PackageJson.Options)
        };
        await using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, true))
            foreach (var item in entries)
            {
                cancellationToken.ThrowIfCancellationRequested(); var entry = archive.CreateEntry(item.Key, CompressionLevel.NoCompression);
                entry.LastWriteTime = Timestamp; await using var stream = entry.Open(); await stream.WriteAsync(item.Value, cancellationToken);
            }
        var bytes = buffer.ToArray(); await destination.WriteAsync(bytes, cancellationToken);
        return (PackageChecksums.Sha256(bytes), bytes.LongLength);
    }
}
