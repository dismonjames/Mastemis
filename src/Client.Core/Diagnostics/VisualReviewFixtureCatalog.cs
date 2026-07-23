namespace Mastemis.Client.Core.Diagnostics;

public static class VisualReviewFixtureCatalog
{
    public static IReadOnlySet<string> Roles { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Administrator", "ExamManager", "ChiefInvigilator", "RoomInvigilator", "Candidate",
        "ProblemOwner", "ProblemEditor", "ProblemReviewer", "ProblemViewer", "EvidenceReviewer"
    };

    public static IReadOnlySet<string> States { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "loading", "empty", "populated", "error", "forbidden", "disconnected", "reconnecting",
        "active", "completed", "failed", "cancelled", "locked", "conflict", "hidden-denied",
        "expired-permission", "terminated", "validation-diagnostics", "upload-progress", "download-progress"
    };
}
