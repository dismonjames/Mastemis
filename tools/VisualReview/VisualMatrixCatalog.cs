namespace Mastemis.VisualReview;

internal static class VisualMatrixCatalog
{
    public static IReadOnlyList<(int Width, int Height)> DesktopSizes { get; } =
        [(1600, 900), (1366, 768), (1024, 768), (900, 700)];

    public static IReadOnlyList<string> DarkRoutes { get; } =
    [
        "onboarding", "connection-success", "connection-unavailable", "login", "login-error",
        "administrator-dashboard", "exam-manager-dashboard", "chief-dashboard", "room-invigilator-dashboard", "candidate-dashboard",
        "examinations", "examination-detail", "rooms", "room-detail", "candidates", "candidate-detail",
        "candidate-workspace", "candidate-terminated", "submissions", "submission-detail", "invigilation",
        "invigilation-candidate-detail", "evidence", "problem-library", "problem-studio-overview",
        "problem-studio-metadata", "problem-studio-statements", "problem-studio-assets", "problem-studio-mas",
        "problem-studio-generation", "problem-studio-reference-solution", "problem-studio-tests", "problem-studio-packages",
        "problem-studio-permissions", "problem-studio-activity", "workers", "health", "settings", "about"
    ];

    public static IReadOnlyList<string> LightRoutes { get; } =
    [
        "onboarding", "login", "administrator-dashboard", "examinations", "candidate-workspace", "candidate-terminated",
        "invigilation", "problem-library", "problem-studio-overview", "problem-studio-metadata", "problem-studio-statements",
        "problem-studio-assets", "problem-studio-mas", "problem-studio-reference-solution", "problem-studio-tests",
        "problem-studio-packages", "workers", "health", "settings", "about"
    ];

    public static string StateFor(string scenario) => scenario switch
    {
        "connection-success" => "completed",
        "connection-unavailable" or "login-error" => "error",
        "candidate-terminated" => "terminated",
        _ => "populated"
    };
}
