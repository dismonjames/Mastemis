namespace Mastemis.Client.Core.Storage;

public sealed record ClientPreferences(string Theme, string Language, string? DefaultServer, int EditorFontSize, int TabSize, int AutosaveSeconds, bool ReducedMotion, bool CompactTables);
public interface IClientPreferenceStore { Task<ClientPreferences> LoadAsync(CancellationToken cancellationToken); Task SaveAsync(ClientPreferences preferences, CancellationToken cancellationToken); }
