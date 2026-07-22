using System.Security.Cryptography;
using Mastemis.Domain;
using Mastemis.Infrastructure;

namespace Mastemis.Infrastructure.Tests;

public sealed class SourceStorageTests
{
    [Fact]
    public async Task Source_is_atomically_named_hashed_and_length_recorded()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mastemis-storage-test-{Guid.NewGuid():N}");
        try
        {
            var content = "using System;"u8.ToArray();
            var stored = await new FileSourceRevisionStorage(root).StoreAsync(SourceRevisionId.New(), content, TestContext.Current.CancellationToken);
            Assert.Equal(content.Length, stored.Length);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), stored.Sha256);
            Assert.StartsWith("source/", stored.ObjectId, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(root, stored.ObjectId)));
            Assert.Empty(Directory.EnumerateFiles(root, ".*.tmp", SearchOption.TopDirectoryOnly));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
