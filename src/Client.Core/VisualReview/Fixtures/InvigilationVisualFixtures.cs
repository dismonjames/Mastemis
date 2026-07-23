namespace Mastemis.Client.Core.VisualReview.Fixtures;

internal static class InvigilationVisualFixtures
{
    public static VisualFixture Create(string scenario, string role, string state) => VisualFixtureBuilder.Create(
        scenario, role, state, "InvigilationViewModel",
        VisualFixtureBuilder.Section("Live overview",
            VisualFixtureBuilder.Value("Rooms", state == "empty" ? 0 : 4, "metric"),
            VisualFixtureBuilder.Value("Connected", state == "empty" ? 0 : 141, "metric"),
            VisualFixtureBuilder.Value("Disconnected", state == "empty" ? 0 : 5, "metric"),
            VisualFixtureBuilder.Value("Confirmed warnings", state == "empty" ? 0 : 3, "metric")),
        VisualFixtureBuilder.Section("Candidate detail",
            VisualFixtureBuilder.Value("Candidate", "Candidate 024"),
            VisualFixtureBuilder.Value("Room", "Room B"),
            VisualFixtureBuilder.Value("Connection", state == "disconnected" ? "Disconnected · stale 4m" : "Connected", "status"),
            VisualFixtureBuilder.Value("Latest event", "Window focus evaluated · no warning")));
}
