using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Core.Navigation;

public enum ClientRoute
{
    Connection, Login, Dashboard, Examinations, Rooms, Candidates, CandidateExam, Submissions,
    Invigilation, Evidence, Problems, ProblemStudio, Workers, Health, Settings, About, Unauthorized, NotFound
}

public sealed record NavigationDescriptor(ClientRoute Route, string Label, string Glyph, string Group, IReadOnlyList<string> Roles);

public interface IClientNavigator
{
    ClientRoute Current { get; }
    event EventHandler<ClientRoute>? RouteChanged;
    void Navigate(ClientRoute route);
}

public sealed class ClientNavigator : IClientNavigator
{
    public ClientRoute Current { get; private set; } = ClientRoute.Connection;
    public event EventHandler<ClientRoute>? RouteChanged;
    public void Navigate(ClientRoute route) { Current = route; RouteChanged?.Invoke(this, route); }
}

public sealed class NavigationCatalog
{
    private static readonly NavigationDescriptor[] Items =
    [
        new(ClientRoute.Dashboard, "Dashboard", "\uE80F", "Overview", []),
        new(ClientRoute.Examinations, "Examinations", "\uE787", "Operations", ["Administrator", "ExamManager", "ChiefInvigilator"]),
        new(ClientRoute.Rooms, "Rooms", "\uE8B7", "Operations", ["Administrator", "ExamManager", "ChiefInvigilator", "RoomInvigilator"]),
        new(ClientRoute.Candidates, "Candidates", "\uE716", "Operations", ["Administrator", "ExamManager", "ChiefInvigilator", "RoomInvigilator"]),
        new(ClientRoute.CandidateExam, "Current examination", "\uE70F", "Examination", ["Candidate"]),
        new(ClientRoute.Submissions, "Submissions", "\uE8A5", "Examination", ["Candidate"]),
        new(ClientRoute.Invigilation, "Invigilation", "\uE7BA", "Live monitoring", ["ChiefInvigilator", "RoomInvigilator"]),
        new(ClientRoute.Evidence, "Evidence", "\uE8D7", "Review", ["EvidenceReviewer"]),
        new(ClientRoute.Problems, "Problems", "\uE82D", "Authoring", ["Administrator", "ExamManager", "ProblemOwner", "ProblemEditor", "ProblemReviewer", "ProblemViewer"]),
        new(ClientRoute.ProblemStudio, "Problem Studio", "\uE943", "Authoring", ["Administrator", "ExamManager", "ProblemOwner", "ProblemEditor", "ProblemReviewer", "ProblemViewer"]),
        new(ClientRoute.Workers, "Judge workers", "\uE950", "System", ["Administrator", "ExamManager"]),
        new(ClientRoute.Health, "System health", "\uE9D9", "System", ["Administrator"]),
        new(ClientRoute.Settings, "Settings", "\uE713", "Preferences", []),
        new(ClientRoute.About, "About", "\uE946", "Preferences", [])
    ];

    public IReadOnlyList<NavigationDescriptor> For(ClientSession session)
        => session.IsAuthenticated
            ? Items.Where(item => item.Roles.Count == 0 || item.Roles.Any(session.Roles.Contains)).ToArray()
            : [];

    public bool IsAuthorized(ClientRoute route, ClientSession session)
        => For(session).Any(item => item.Route == route);
}
