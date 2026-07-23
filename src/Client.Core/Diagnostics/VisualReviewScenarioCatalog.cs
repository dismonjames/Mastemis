using Mastemis.Client.Core.Navigation;

namespace Mastemis.Client.Core.Diagnostics;

public static class VisualReviewScenarioCatalog
{
    private static readonly IReadOnlyDictionary<string, VisualReviewScenario> Scenarios = Create();

    public static IReadOnlyCollection<VisualReviewScenario> All => Scenarios.Values.Distinct().ToArray();

    public static bool TryResolve(string name, out VisualReviewScenario scenario) =>
        Scenarios.TryGetValue(Normalize(name), out scenario!);

    private static Dictionary<string, VisualReviewScenario> Create()
    {
        var values = new Dictionary<string, VisualReviewScenario>(StringComparer.OrdinalIgnoreCase);
        Add(values, "onboarding", ClientRoute.Connection, "Administrator", aliases: ["connection", "connection-unavailable", "connection-success"]);
        Add(values, "login", ClientRoute.Login, "Administrator", aliases: ["login-error"]);
        Add(values, "administrator-dashboard", ClientRoute.Dashboard, "Administrator", aliases: ["dashboard"]);
        Add(values, "exam-manager-dashboard", ClientRoute.Dashboard, "ExamManager");
        Add(values, "chief-dashboard", ClientRoute.Dashboard, "ChiefInvigilator");
        Add(values, "room-invigilator-dashboard", ClientRoute.Dashboard, "RoomInvigilator");
        Add(values, "candidate-dashboard", ClientRoute.Dashboard, "Candidate");
        Add(values, "examinations", ClientRoute.Examinations, "ExamManager", aliases: ["examination-detail"]);
        Add(values, "rooms", ClientRoute.Rooms, "ChiefInvigilator", aliases: ["room-detail"]);
        Add(values, "candidates", ClientRoute.Candidates, "RoomInvigilator", aliases: ["candidate-detail"]);
        Add(values, "candidate-workspace", ClientRoute.CandidateExam, "Candidate", aliases: ["candidate-terminated"]);
        Add(values, "submissions", ClientRoute.Submissions, "Candidate", aliases: ["submission-detail"]);
        Add(values, "invigilation", ClientRoute.Invigilation, "ChiefInvigilator", aliases: ["invigilation-candidate-detail"]);
        Add(values, "evidence", ClientRoute.Evidence, "EvidenceReviewer");
        Add(values, "problem-library", ClientRoute.Problems, "ProblemViewer", aliases: ["problems"]);
        AddProblemStudio(values, "problem-studio", 0, aliases: ["problem-studio-overview"]);
        AddProblemStudio(values, "problem-studio-metadata", 1);
        AddProblemStudio(values, "problem-studio-statements", 2);
        AddProblemStudio(values, "problem-studio-mas", 3);
        AddProblemStudio(values, "problem-studio-diagnostics", 4);
        AddProblemStudio(values, "problem-studio-generation", 5);
        AddProblemStudio(values, "problem-studio-assets", 6);
        AddProblemStudio(values, "problem-studio-reference-solution", 7);
        AddProblemStudio(values, "problem-studio-tests", 8);
        AddProblemStudio(values, "problem-studio-packages", 9);
        AddProblemStudio(values, "problem-studio-permissions", 10);
        AddProblemStudio(values, "problem-studio-activity", 11);
        Add(values, "workers", ClientRoute.Workers, "Administrator");
        Add(values, "health", ClientRoute.Health, "Administrator");
        Add(values, "settings", ClientRoute.Settings, "Administrator");
        Add(values, "about", ClientRoute.About, "Administrator");
        return values;
    }

    private static void AddProblemStudio(Dictionary<string, VisualReviewScenario> values, string name, int section, params string[] aliases) =>
        Add(values, name, ClientRoute.ProblemStudio, "ProblemOwner", section, aliases);

    private static void Add(Dictionary<string, VisualReviewScenario> values, string name, ClientRoute route,
        string role, int? section = null, params string[] aliases)
    {
        var scenario = new VisualReviewScenario(name, route, role, section);
        values[Normalize(name)] = scenario;
        foreach (var alias in aliases) values[Normalize(alias)] = scenario with { Name = alias };
    }

    private static string Normalize(string value) => value.Trim().Replace('_', '-').ToLowerInvariant();
}
