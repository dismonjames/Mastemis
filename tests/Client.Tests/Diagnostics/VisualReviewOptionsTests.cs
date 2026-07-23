using Mastemis.Client.Core.Diagnostics;
using Mastemis.Client.Core.Navigation;

namespace Mastemis.Client.Tests.Diagnostics;

public sealed class VisualReviewOptionsTests
{
    [Fact]
    public void Review_route_and_dimensions_are_deterministic()
    {
        var options = VisualReviewOptions.Parse(["--visual-review", "problem-studio", "--width", "1024", "--height", "768", "--role", "ProblemOwner"], true);
        Assert.NotNull(options);
        Assert.Equal(ClientRoute.ProblemStudio, options.Route);
        Assert.Equal(1024, options.Width);
        Assert.Equal("ProblemOwner", options.Role);
    }

    [Fact]
    public void Review_mode_fails_closed_without_explicit_environment_enablement() =>
        Assert.Throws<InvalidOperationException>(() => VisualReviewOptions.Parse(["--visual-review", "dashboard"], false));
}
