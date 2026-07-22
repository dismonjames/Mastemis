using Mastemis.Application;

namespace Mastemis.Infrastructure.Storage.ProblemObjects;

internal static class ProblemObjectPath
{
    public static string Resolve(string root, string objectId, bool staged)
    {
        if (!TryParse(objectId, out var category, out var identifier)) throw Unsafe();
        var canonicalRoot = Path.GetFullPath(root);
        var state = staged ? ".staged" : "objects";
        var path = Path.GetFullPath(Path.Combine(canonicalRoot, state, category, $"{identifier}.bin"));
        if (!path.StartsWith(canonicalRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw Unsafe();
        return path;
    }

    public static bool TryParse(string objectId, out string category, out string identifier)
    {
        category = string.Empty;
        identifier = string.Empty;
        var segments = objectId.Split('/');
        if (segments.Length != 3 || segments[0] != "problem" || !Guid.TryParseExact(segments[2], "N", out _)) return false;
        if (segments[1] is not ("asset" or "package" or "test-input" or "expected-output" or "reference-source" or "export")) return false;
        category = segments[1];
        identifier = segments[2];
        return true;
    }

    private static ApplicationFailure Unsafe() => new(ErrorCodes.InvalidInput, "Unsafe problem object identifier.");
}
