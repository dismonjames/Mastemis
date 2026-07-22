using System.Security.Cryptography;

namespace Mastemis.Mas.Packaging.Checksums;

public static class PackageChecksums
{
    public static string Sha256(ReadOnlySpan<byte> content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
