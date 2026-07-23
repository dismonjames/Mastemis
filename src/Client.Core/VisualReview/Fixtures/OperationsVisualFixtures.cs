namespace Mastemis.Client.Core.VisualReview.Fixtures;

internal static class OperationsVisualFixtures
{
    public static VisualFixture Create(string scenario, string role, string state)
    {
        var viewModel = scenario.StartsWith("examination", StringComparison.Ordinal) ? "ExaminationViewModel"
            : scenario.StartsWith("room", StringComparison.Ordinal) ? "RoomOperationsViewModel"
            : scenario.StartsWith("candidate", StringComparison.Ordinal) ? "CandidateOperationsViewModel"
            : scenario.StartsWith("submission", StringComparison.Ordinal) ? "CandidateWorkspaceViewModel"
            : "EvidenceViewModel";
        return VisualFixtureBuilder.Create(scenario, role, state, viewModel,
            VisualFixtureBuilder.Section("Current scope",
                VisualFixtureBuilder.Value("Examination", "National Programming Final"),
                VisualFixtureBuilder.Value("Status", state == "completed" ? "Completed" : "Active", "status"),
                VisualFixtureBuilder.Value("Window", "09:00–12:00 local time")),
            VisualFixtureBuilder.Section("Results",
                VisualFixtureBuilder.Value("Rooms", state == "empty" ? 0 : 4, "metric"),
                VisualFixtureBuilder.Value("Candidates", state == "empty" ? 0 : 146, "metric"),
                VisualFixtureBuilder.Value("Warnings", state == "empty" ? 0 : 3, "metric")));
    }
}
