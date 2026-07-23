using System.Windows.Input;
using Mastemis.Client.Core.Common;
using Mastemis.Client.Core.Common.Commands;
using Mastemis.Client.Core.Storage;

namespace Mastemis.Client.Core.Features.Settings;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly IClientPreferenceStore store; private string theme = "System"; private string language = "en"; private int editorFontSize = 14; private int tabSize = 4; private int autosaveSeconds = 5; private bool reducedMotion; private bool compactTables; private string status = string.Empty;
    public SettingsViewModel(IClientPreferenceStore store) { this.store = store; LoadCommand = new AsyncCommand(LoadAsync); SaveCommand = new AsyncCommand(SaveAsync); }
    public IReadOnlyList<string> Themes { get; } = ["System", "Light", "Dark"]; public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public event EventHandler<string>? ThemeChanged;
    public string Theme { get => theme; set { if (SetProperty(ref theme, Themes.Contains(value) ? value : "System")) ThemeChanged?.Invoke(this, theme); } }
    public string Language { get => language; set => SetProperty(ref language, value); }
    public int EditorFontSize { get => editorFontSize; set => SetProperty(ref editorFontSize, Math.Clamp(value, 10, 28)); }
    public int TabSize { get => tabSize; set => SetProperty(ref tabSize, Math.Clamp(value, 2, 8)); }
    public int AutosaveSeconds { get => autosaveSeconds; set => SetProperty(ref autosaveSeconds, Math.Clamp(value, 2, 60)); }
    public bool ReducedMotion { get => reducedMotion; set => SetProperty(ref reducedMotion, value); }
    public bool CompactTables { get => compactTables; set => SetProperty(ref compactTables, value); }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    private async Task LoadAsync(CancellationToken ct) { var value = await store.LoadAsync(ct).ConfigureAwait(true); Theme = value.Theme; Language = value.Language; EditorFontSize = value.EditorFontSize; TabSize = value.TabSize; AutosaveSeconds = value.AutosaveSeconds; ReducedMotion = value.ReducedMotion; CompactTables = value.CompactTables; }
    private async Task SaveAsync(CancellationToken ct) { await store.SaveAsync(new(Theme, Language, null, EditorFontSize, TabSize, AutosaveSeconds, ReducedMotion, CompactTables), ct).ConfigureAwait(true); Status = "Preferences saved locally"; }
}
