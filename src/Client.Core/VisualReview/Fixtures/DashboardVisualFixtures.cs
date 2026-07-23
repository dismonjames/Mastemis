namespace Mastemis.Client.Core.VisualReview.Fixtures;

internal static class DashboardVisualFixtures
{
    public static VisualFixture Create(string scenario, string role, string state) => VisualFixtureBuilder.Create(
        scenario, role, state, "DashboardViewModel",
        VisualFixtureBuilder.Section("Operations",
            VisualFixtureBuilder.Value("Active examinations", state == "empty" ? 0 : 2, "metric"),
            VisualFixtureBuilder.Value("Connected candidates", state == "empty" ? 0 : 146, "metric"),
            VisualFixtureBuilder.Value("Pending judgements", state == "empty" ? 0 : 7, "metric"),
            VisualFixtureBuilder.Value("Worker capacity", state == "degraded" ? "3 / 8" : "3 / 12", "metric")),
        VisualFixtureBuilder.Section("Recent activity",
            VisualFixtureBuilder.Value("08:28", "Room B reconnected"),
            VisualFixtureBuilder.Value("08:24", "Generation operation completed"),
            VisualFixtureBuilder.Value("08:19", "Warning confirmed in room A")));
}
