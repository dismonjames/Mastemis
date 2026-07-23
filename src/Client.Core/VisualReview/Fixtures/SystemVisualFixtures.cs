namespace Mastemis.Client.Core.VisualReview.Fixtures;

internal static class SystemVisualFixtures
{
    public static VisualFixture Create(string scenario, string role, string state)
    {
        var viewModel = scenario == "workers" ? "WorkerOperationsViewModel"
            : scenario == "health" ? "HealthViewModel"
            : scenario == "settings" ? "SettingsViewModel" : "AboutViewModel";
        return VisualFixtureBuilder.Create(scenario, role, state, viewModel,
            VisualFixtureBuilder.Section("System",
                VisualFixtureBuilder.Value("Server", "Mastemis Preview"),
                VisualFixtureBuilder.Value("PostgreSQL", state == "degraded" ? "Degraded" : "Ready", "status"),
                VisualFixtureBuilder.Value("Outbox", state == "degraded" ? "Backlog: 18" : "Ready", "status"),
                VisualFixtureBuilder.Value("Workers", state == "empty" ? "No workers" : "3 ready · 12 total capacity", "status")),
            VisualFixtureBuilder.Section("Runtime",
                VisualFixtureBuilder.Value("Version", "2026.7"),
                VisualFixtureBuilder.Value("License", "Mozilla Public License 2.0"),
                VisualFixtureBuilder.Value("Author", "Lê Hùng Quang Minh")));
    }
}
