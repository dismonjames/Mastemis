namespace Mastemis.Client.Core.Platform.Files;

public sealed record ClientFile(string Name, string ContentType, long Length, Func<CancellationToken, Task<Stream>> OpenReadAsync);

public interface IClientFileService
{
    Task<ClientFile?> PickOpenAsync(IReadOnlyList<string> extensions, CancellationToken cancellationToken);
    Task<ClientFile?> OpenDroppedAsync(object platformFile, IReadOnlyList<string> extensions, CancellationToken cancellationToken);
    Task SaveAsync(string suggestedName, Stream content, CancellationToken cancellationToken);
}
