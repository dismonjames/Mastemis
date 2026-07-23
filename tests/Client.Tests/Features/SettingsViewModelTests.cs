using Mastemis.Client.Core.Features.Settings;
using Mastemis.Client.Core.Storage;

namespace Mastemis.Client.Tests.Features;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void Theme_change_is_announced_immediately()
    {
        var viewModel = new SettingsViewModel(new MemoryPreferences());
        string? announced = null;
        viewModel.ThemeChanged += (_, value) => announced = value;
        viewModel.Theme = "Light";
        Assert.Equal("Light", announced);
    }

    [Fact]
    public void Unknown_theme_falls_back_to_system()
    {
        var viewModel = new SettingsViewModel(new MemoryPreferences());
        viewModel.Theme = "unknown";
        Assert.Equal("System", viewModel.Theme);
    }

    [Fact]
    public void Reduced_motion_applies_immediately_without_hiding_state()
    {
        var viewModel = new SettingsViewModel(new MemoryPreferences());
        bool? announced = null;
        viewModel.MotionPreferenceChanged += (_, value) => announced = value;

        viewModel.ReducedMotion = true;

        Assert.True(announced);
        Assert.False(viewModel.AnimationsEnabled);
    }

    private sealed class MemoryPreferences : IClientPreferenceStore
    {
        public Task<ClientPreferences> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ClientPreferences("System", "en", null, 14, 4, 5, false, false));
        public Task SaveAsync(ClientPreferences preferences, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
