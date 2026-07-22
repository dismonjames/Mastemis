namespace Mastemis.Mas.Packaging.Security;

public sealed record PackageArchiveLimits(long MaximumCompressedBytes = 64 * 1024 * 1024,
    long MaximumDecompressedBytes = 512 * 1024 * 1024, int MaximumEntries = 20_000,
    long MaximumEntryBytes = 64 * 1024 * 1024, int MaximumPathLength = 240,
    long MaximumManifestBytes = 1024 * 1024, double MaximumCompressionRatio = 200);
