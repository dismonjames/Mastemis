using Mastemis.Application;

namespace Mastemis.Infrastructure.Storage.SourceRevisions;

public static class SourceObjectPath
{
    public static string Resolve(string rootPath, string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId) || Path.IsPathRooted(objectId)) throw Unsafe();
        var root = Path.GetFullPath(rootPath);
        var path = Path.GetFullPath(Path.Combine(root, objectId.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw Unsafe();
        return path;
    }

    public static bool IsGeneratedSourceObject(string objectId)
    {
        if (!objectId.StartsWith("source/", StringComparison.Ordinal) || !objectId.EndsWith(".bin", StringComparison.Ordinal)) return false;
        var name = objectId[7..^4]; return name.Length == 32 && name.All(Uri.IsHexDigit);
    }
    private static ApplicationFailure Unsafe() => new(ErrorCodes.InvalidInput, "Unsafe storage path.");
}
