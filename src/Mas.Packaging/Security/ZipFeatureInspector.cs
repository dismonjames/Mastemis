using System.Buffers.Binary;
using Mastemis.Mas.Packaging.Validation;

namespace Mastemis.Mas.Packaging.Security;

public static class ZipFeatureInspector
{
    private const uint CentralSignature = 0x02014b50; private const uint LocalSignature = 0x04034b50; private const uint EndSignature = 0x06054b50;
    public static void Validate(ReadOnlySpan<byte> bytes)
    {
        var end = FindEnd(bytes); if (end < 0 || end + 22 > bytes.Length) Fail("package.archive.metadata");
        var commentLength = Read16(bytes, end + 20); if (end + 22 + commentLength != bytes.Length) Fail("package.archive.trailing_data");
        var count = Read16(bytes, end + 10); var centralOffset = checked((int)Read32(bytes, end + 16)); var position = centralOffset;
        for (var index = 0; index < count; index++)
        {
            if (position + 46 > bytes.Length || Read32(bytes, position) != CentralSignature) Fail("package.archive.central_directory");
            var flags = Read16(bytes, position + 8); var method = Read16(bytes, position + 10);
            if ((flags & 0x41) != 0) Fail("package.archive.encrypted"); if (method is not (0 or 8)) Fail("package.archive.compression_method");
            var nameLength = Read16(bytes, position + 28); var extraLength = Read16(bytes, position + 30); var entryComment = Read16(bytes, position + 32);
            var localOffset = checked((int)Read32(bytes, position + 42));
            if (localOffset + 30 > bytes.Length || Read32(bytes, localOffset) != LocalSignature || Read16(bytes, localOffset + 6) != flags || Read16(bytes, localOffset + 8) != method)
                Fail("package.archive.header_mismatch");
            position = checked(position + 46 + nameLength + extraLength + entryComment);
        }
    }
    private static int FindEnd(ReadOnlySpan<byte> bytes)
    { for (var i = bytes.Length - 22; i >= Math.Max(0, bytes.Length - 65_557); i--) if (Read32(bytes, i) == EndSignature) return i; return -1; }
    private static ushort Read16(ReadOnlySpan<byte> bytes, int offset) => offset + 2 <= bytes.Length ? BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]) : (ushort)0;
    private static uint Read32(ReadOnlySpan<byte> bytes, int offset) => offset + 4 <= bytes.Length ? BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]) : 0;
    private static void Fail(string code) => throw new PackageValidationException([new(code, PackageDiagnosticSeverity.Error, "ZIP feature or metadata is unsupported.")]);
}
