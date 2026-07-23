using Mastemis.Client.Core.Responsive;

namespace Mastemis.Client.Tests.Responsive;

public sealed class DesktopLayoutProfileTests
{
    [Theory]
    [InlineData(1600, 1, DesktopWidthClass.Wide)]
    [InlineData(1366, 1, DesktopWidthClass.Wide)]
    [InlineData(1024, 1, DesktopWidthClass.Medium)]
    [InlineData(900, 1, DesktopWidthClass.Compact)]
    [InlineData(1600, 2, DesktopWidthClass.Compact)]
    public void Width_and_text_scale_select_a_deterministic_profile(double width, double scale, DesktopWidthClass expected)
    {
        var profile = DesktopLayoutProfile.Select(width, scale);
        Assert.Equal(expected, profile.WidthClass);
        Assert.Equal(expected != DesktopWidthClass.Wide, profile.IsNavigationCompact);
    }
}
