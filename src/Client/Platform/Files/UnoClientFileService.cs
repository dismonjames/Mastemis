using Mastemis.Client.Core.Platform.Files;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Mastemis.Client.Platform.Files;

public sealed class UnoClientFileService : IClientFileService
{
    public async Task<ClientFile?> PickOpenAsync(IReadOnlyList<string> extensions, CancellationToken cancellationToken)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        foreach (var extension in extensions) picker.FileTypeFilter.Add(extension);
        var file = await picker.PickSingleFileAsync();
        if (file is null) return null;
        var properties = await file.GetBasicPropertiesAsync();
        return new(file.Name, ContentType(file.FileType), checked((long)properties.Size), async ct =>
        {
            ct.ThrowIfCancellationRequested();
            return (Stream)await file.OpenStreamForReadAsync();
        });
    }

    public async Task SaveAsync(string suggestedName, Stream content, CancellationToken cancellationToken)
    {
        var picker = new FileSavePicker { SuggestedFileName = suggestedName, SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeChoices.Add("Mastemis file", [Path.GetExtension(suggestedName) is { Length: > 0 } ext ? ext : ".bin"]);
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        await using var target = await file.OpenStreamForWriteAsync();
        target.SetLength(0);
        await content.CopyToAsync(target, cancellationToken);
    }

    public async Task<ClientFile?> OpenDroppedAsync(object platformFile, IReadOnlyList<string> extensions, CancellationToken cancellationToken)
    {
        if (platformFile is not StorageFile file || !extensions.Contains(file.FileType, StringComparer.OrdinalIgnoreCase)) return null;
        cancellationToken.ThrowIfCancellationRequested();
        var properties = await file.GetBasicPropertiesAsync();
        return new(file.Name, ContentType(file.FileType), checked((long)properties.Size), async ct =>
        {
            ct.ThrowIfCancellationRequested();
            return (Stream)await file.OpenStreamForReadAsync();
        });
    }

    private static string ContentType(string extension) => extension.ToLowerInvariant() switch
    { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".svg" => "image/svg+xml", ".pdf" => "application/pdf", ".mas" => "application/vnd.mastemis.problem+zip", _ => "text/plain" };
}
