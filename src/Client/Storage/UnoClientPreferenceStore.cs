using System.Text.Json;
using Mastemis.Client.Core.Storage;
using Windows.Storage;

namespace Mastemis.Client.Storage;

public sealed class UnoClientPreferenceStore : IClientPreferenceStore
{
    private const string Key = "client.preferences.v1";
    private static readonly ClientPreferences Defaults = new("System", "en", null, 14, 4, 5, false, false);
    public Task<ClientPreferences> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = ApplicationData.Current.LocalSettings.Values.TryGetValue(Key, out var value) ? value as string : null;
        try { return Task.FromResult(json is null ? Defaults : JsonSerializer.Deserialize<ClientPreferences>(json) ?? Defaults); }
        catch (JsonException) { return Task.FromResult(Defaults); }
    }
    public Task SaveAsync(ClientPreferences preferences, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ApplicationData.Current.LocalSettings.Values[Key] = JsonSerializer.Serialize(preferences with { DefaultServer = preferences.DefaultServer });
        return Task.CompletedTask;
    }
}
