namespace Mastemis.Client.Core.VisualReview.Fixtures;

internal static class AccessVisualFixtures
{
    public static VisualFixture Create(string scenario, string role, string state)
    {
        var connection = scenario.StartsWith("onboarding", StringComparison.Ordinal) || scenario.StartsWith("connection", StringComparison.Ordinal);
        return VisualFixtureBuilder.Create(scenario, role, state,
            connection ? "ConnectionViewModel" : "LoginViewModel",
            VisualFixtureBuilder.Section(connection ? "Server setup" : "Authentication",
                VisualFixtureBuilder.Value("Server", "Mastemis Preview"),
                VisualFixtureBuilder.Value("Address", "https://review.invalid"),
                VisualFixtureBuilder.Value("Version", "2026.7"),
                VisualFixtureBuilder.Value("State", StateLabel(state), "status")),
            VisualFixtureBuilder.Section("Security",
                VisualFixtureBuilder.Value("Transport", "HTTPS required"),
                VisualFixtureBuilder.Value("Credentials", "Not stored")));
    }

    private static string StateLabel(string state) => state switch
    {
        "error" => "Invalid credentials",
        "disconnected" => "Server unavailable",
        "failed" => "Incompatible server version",
        "completed" => "Compatible · authentication required",
        "active" => "Connecting…",
        _ => "Ready to connect"
    };
}
