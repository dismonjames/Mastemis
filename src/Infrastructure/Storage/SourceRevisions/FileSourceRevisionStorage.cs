using System.Security.Cryptography;
using Mastemis.Application;
using Mastemis.Domain;

namespace Mastemis.Infrastructure.Storage.SourceRevisions;

public sealed class FileSourceRevisionStorage(string rootPath) : ISourceRevisionStorage
{
    private readonly string _root = Path.GetFullPath(rootPath);

    public async Task<StoredSourceRevision> StoreAsync(SourceRevisionId id, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_root);
        var objectId = $"source/{id.Value:N}.bin";
        var finalPath = SourceObjectPath.Resolve(_root, objectId);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
        var temporaryPath = SourceObjectPath.Resolve(_root, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, content.ToArray(), cancellationToken);
            File.Move(temporaryPath, finalPath, false);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
        return new StoredSourceRevision(objectId, Convert.ToHexString(SHA256.HashData(content.Span)).ToLowerInvariant(), content.Length);
    }
}
