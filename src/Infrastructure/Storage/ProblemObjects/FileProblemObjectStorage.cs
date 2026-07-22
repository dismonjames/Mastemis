using System.Security.Cryptography;
using Mastemis.Application;
using Mastemis.Application.Problems.Assets;

namespace Mastemis.Infrastructure.Storage.ProblemObjects;

public sealed class FileProblemObjectStorage(string rootPath, IClock clock) : IProblemObjectStorage
{
    private readonly string _root = Path.GetFullPath(rootPath);

    public async Task<StagedProblemObject> StageAsync(ProblemObjectKind kind, Stream content, long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (maximumBytes <= 0) throw new ApplicationFailure(ErrorCodes.InvalidInput, "Object size limit is invalid.");
        var objectId = $"problem/{Category(kind)}/{Guid.NewGuid():N}";
        var target = ProblemObjectPath.Resolve(_root, objectId, true);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = $"{target}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long length = 0;
            while (true)
            {
                var read = await content.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;
                length = checked(length + read);
                if (length > maximumBytes) throw new ApplicationFailure(ErrorCodes.InvalidInput, "Object exceeds its size limit.");
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            await output.FlushAsync(cancellationToken);
            File.Move(temporary, target, false);
            return new(objectId, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), length, clock.UtcNow);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public Task MarkReferencedAsync(string objectId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var staged = ProblemObjectPath.Resolve(_root, objectId, true);
        var committed = ProblemObjectPath.Resolve(_root, objectId, false);
        if (File.Exists(committed)) return Task.CompletedTask;
        if (!File.Exists(staged)) throw new ApplicationFailure(ErrorCodes.NotFound, "Staged object was not found.");
        Directory.CreateDirectory(Path.GetDirectoryName(committed)!);
        File.Move(staged, committed, false);
        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string objectId, long maximumBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ProblemObjectPath.Resolve(_root, objectId, false);
        if (!File.Exists(path)) throw new ApplicationFailure(ErrorCodes.NotFound, "Problem object was not found.");
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        if (stream.Length > maximumBytes) { stream.Dispose(); throw new ApplicationFailure(ErrorCodes.InvalidInput, "Object exceeds its read limit."); }
        return Task.FromResult<Stream>(stream);
    }

    public Task DeleteStagedAsync(string objectId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ProblemObjectPath.Resolve(_root, objectId, true);
        try { File.Delete(path); } catch (DirectoryNotFoundException) { }
        return Task.CompletedTask;
    }

    private static string Category(ProblemObjectKind kind) => kind switch
    {
        ProblemObjectKind.Asset => "asset",
        ProblemObjectKind.Statement => "statement",
        ProblemObjectKind.Package => "package",
        ProblemObjectKind.TestInput => "test-input",
        ProblemObjectKind.ExpectedOutput => "expected-output",
        ProblemObjectKind.ReferenceSource => "reference-source",
        ProblemObjectKind.Export => "export",
        _ => throw new ApplicationFailure(ErrorCodes.InvalidInput, "Unsupported problem object kind.")
    };
}
