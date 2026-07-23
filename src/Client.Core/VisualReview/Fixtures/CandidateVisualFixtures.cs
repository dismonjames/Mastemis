namespace Mastemis.Client.Core.VisualReview.Fixtures;

internal static class CandidateVisualFixtures
{
    public static VisualFixture Create(string scenario, string role, string state) => VisualFixtureBuilder.Create(
        scenario, role, state, "CandidateWorkspaceViewModel",
        VisualFixtureBuilder.Section("Examination",
            VisualFixtureBuilder.Value("Title", "National Programming Final"),
            VisualFixtureBuilder.Value("Remaining", "01:42:18", "status"),
            VisualFixtureBuilder.Value("Session", state == "terminated" ? "Terminated after third warning" : "Active", "status")),
        VisualFixtureBuilder.Section("Problem A · Balanced sequence",
            VisualFixtureBuilder.Value("Limit", "1 second · 256 MiB"),
            VisualFixtureBuilder.Value("Draft", state == "active" ? "Autosaving…" : "Saved at 10:14"),
            VisualFixtureBuilder.Value("Latest result", state switch { "failed" => "Compilation Error", "completed" => "Accepted", _ => "Judging" }, "status")));
}
