namespace Mastemis.Client.Core.VisualReview.Fixtures;

internal static class ProblemVisualFixtures
{
    public static VisualFixture Create(string scenario, string role, string state)
    {
        var studio = scenario.StartsWith("problem-studio", StringComparison.Ordinal);
        return VisualFixtureBuilder.Create(scenario, role, state, studio ? "ProblemStudioViewModel" : "ProblemLibraryViewModel",
            VisualFixtureBuilder.Section(studio ? SectionName(scenario) : "Problem library",
                VisualFixtureBuilder.Value("Problem", "Balanced sequence"),
                VisualFixtureBuilder.Value("Identifier", "balanced-sequence"),
                VisualFixtureBuilder.Value("Revision", 12),
                VisualFixtureBuilder.Value("Permission", role.Replace("Problem", string.Empty), "status")),
            VisualFixtureBuilder.Section("Authoring state",
                VisualFixtureBuilder.Value("Locales", "English · Vietnamese"),
                VisualFixtureBuilder.Value("Languages", "C++23 · C#"),
                VisualFixtureBuilder.Value("Tests", state == "hidden-denied" ? "24 tests · hidden preview denied" : "24 tests · 3 groups"),
                VisualFixtureBuilder.Value("Operation", Operation(state), "status")));
    }

    private static string SectionName(string scenario) => scenario.Replace("problem-studio-", string.Empty).Replace('-', ' ');
    private static string Operation(string state) => state switch
    {
        "locked" => "Locked by active examination",
        "conflict" => "Revision conflict",
        "upload-progress" => "Uploading · 3.2 / 8.0 MiB",
        "download-progress" => "Downloading · 6.7 / 12.0 MiB",
        "validation-diagnostics" => "2 validation diagnostics",
        "expired-permission" => "Assignment expired",
        _ => "Ready"
    };
}
