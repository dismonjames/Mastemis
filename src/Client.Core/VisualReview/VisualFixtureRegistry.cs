using Mastemis.Client.Core.Diagnostics;
using Mastemis.Client.Core.VisualReview.Fixtures;

namespace Mastemis.Client.Core.VisualReview;

public sealed class VisualFixtureRegistry
{
    public VisualFixture Resolve(VisualReviewOptions options)
    {
        var scenario = options.Scenario;
        if (scenario.StartsWith("onboarding", StringComparison.Ordinal) || scenario.StartsWith("connection", StringComparison.Ordinal) || scenario.StartsWith("login", StringComparison.Ordinal))
            return AccessVisualFixtures.Create(scenario, options.Role, options.State);
        if (scenario.Contains("dashboard", StringComparison.Ordinal)) return DashboardVisualFixtures.Create(scenario, options.Role, options.State);
        if (scenario is "candidate-workspace" or "candidate-terminated") return CandidateVisualFixtures.Create(scenario, options.Role, options.State);
        if (scenario.StartsWith("invigilation", StringComparison.Ordinal)) return InvigilationVisualFixtures.Create(scenario, options.Role, options.State);
        if (scenario.StartsWith("problem", StringComparison.Ordinal)) return ProblemVisualFixtures.Create(scenario, options.Role, options.State);
        if (scenario is "workers" or "health" or "settings" or "about") return SystemVisualFixtures.Create(scenario, options.Role, options.State);
        return OperationsVisualFixtures.Create(scenario, options.Role, options.State);
    }
}
