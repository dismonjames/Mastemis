using Mastemis.Client.Core.Diagnostics;
using Mastemis.Client.Core.VisualReview;

namespace Mastemis.Client.Tests.VisualReview;

public sealed class VisualFixtureCoverageTests
{
    private readonly VisualFixtureRegistry registry = new();

    [Fact]
    public void Every_configured_scenario_resolves_to_bounded_deterministic_fixture_data()
    {
        foreach (var name in VisualReviewScenarioCatalog.Names)
        {
            Assert.True(VisualReviewScenarioCatalog.TryResolve(name, out var scenario));
            var first = registry.Resolve(Options(scenario, name, scenario.DefaultRole, "populated"));
            var second = registry.Resolve(Options(scenario, name, scenario.DefaultRole, "populated"));

            Assert.Equal(first.Scenario, second.Scenario);
            Assert.Equal(first.Role, second.Role);
            Assert.Equal(first.State, second.State);
            Assert.Equal(first.TimestampUtc, second.TimestampUtc);
            Assert.Equal(
                first.Sections.SelectMany(section => section.Values.Select(value => $"{section.Heading}|{value.Label}|{value.Value}|{value.Kind}")),
                second.Sections.SelectMany(section => section.Values.Select(value => $"{section.Heading}|{value.Label}|{value.Value}|{value.Kind}")));
            Assert.NotEmpty(first.ExpectedViewModel);
            Assert.NotEmpty(first.Sections);
            Assert.All(first.Sections, section => Assert.NotEmpty(section.Values));
            Assert.All(first.Values, value => Assert.InRange(value.Value.Length, 1, VisualFixture.MaximumValueLength));
        }
    }

    [Theory]
    [MemberData(nameof(RolesAndStates))]
    public void Every_required_role_and_state_produces_a_fixture(string role, string state)
    {
        Assert.True(VisualReviewScenarioCatalog.TryResolve("problem-studio-overview", out var scenario));
        var fixture = registry.Resolve(Options(scenario, scenario.Name, role, state));
        Assert.Equal(role, fixture.Role);
        Assert.Equal(state, fixture.State);
    }

    [Fact]
    public void Fixture_labels_reject_sensitive_or_artifact_categories()
    {
        var forbidden = new[] { "password", "cookie", "secret", "physical path", "expected output", "hidden input", "candidate source" };
        foreach (var name in VisualReviewScenarioCatalog.Names)
        {
            VisualReviewScenarioCatalog.TryResolve(name, out var scenario);
            var fixture = registry.Resolve(Options(scenario!, name, scenario!.DefaultRole, "populated"));
            var text = string.Join('\n', fixture.Values.SelectMany(value => new[] { value.Label, value.Value }));
            Assert.DoesNotContain(forbidden, word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static IEnumerable<object[]> RolesAndStates()
    {
        foreach (var role in VisualReviewFixtureCatalog.Roles)
            foreach (var state in VisualReviewFixtureCatalog.States)
                yield return [role, state];
    }

    private static VisualReviewOptions Options(VisualReviewScenario scenario, string name, string role, string state) =>
        new(scenario.Route, role, 1366, 768, "dark", name, state, scenario.ProblemStudioSection, 1, false);
}
