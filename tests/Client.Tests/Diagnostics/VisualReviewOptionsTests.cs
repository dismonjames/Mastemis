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
        Assert.Equal(0, options.ProblemStudioSection);
    }

    [Fact]
    public void Review_mode_fails_closed_without_explicit_environment_enablement() =>
        Assert.Throws<InvalidOperationException>(() => VisualReviewOptions.Parse(["--visual-review", "dashboard"], false));

    [Theory]
    [InlineData("problem-studio-statements", ClientRoute.ProblemStudio, 2, "ProblemOwner")]
    [InlineData("candidate-terminated", ClientRoute.CandidateExam, null, "Candidate")]
    [InlineData("invigilation-candidate-detail", ClientRoute.Invigilation, null, "ChiefInvigilator")]
    [InlineData("room-invigilator-dashboard", ClientRoute.Dashboard, null, "RoomInvigilator")]
    public void Named_scenarios_resolve_to_isolated_routes(string name, ClientRoute route, int? section, string role)
    {
        var options = VisualReviewOptions.Parse(["--visual-review", name], true);
        Assert.NotNull(options);
        Assert.Equal(route, options.Route);
        Assert.Equal(section, options.ProblemStudioSection);
        Assert.Equal(role, options.Role);
    }

    [Fact]
    public void Review_preferences_are_bounded_and_explicit()
    {
        var options = VisualReviewOptions.Parse(["--visual-review", "workers", "--state", "reconnecting",
            "--theme", "light", "--text-scale", "1.5", "--reduced-motion"], true);
        Assert.NotNull(options);
        Assert.Equal("reconnecting", options.State);
        Assert.Equal("light", options.Theme);
        Assert.Equal(1.5, options.TextScale);
        Assert.True(options.ReducedMotion);
    }

    [Fact]
    public void Every_declared_scenario_round_trips()
    {
        Assert.All(VisualReviewScenarioCatalog.All, scenario =>
        {
            var options = VisualReviewOptions.Parse(["--visual-review", scenario.Name], true);
            Assert.NotNull(options);
            Assert.Equal(scenario.Route, options.Route);
        });
    }
}
