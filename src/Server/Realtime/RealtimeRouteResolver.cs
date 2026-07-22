using System.Text.Json;
using Mastemis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Server.Realtime;

public sealed class RealtimeRouteResolver(MastemisDbContext db)
{
    public async Task<IReadOnlySet<string>> ResolveAsync(string payload, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var targets = new HashSet<string>(StringComparer.Ordinal);
        if (TryGuid(root, "SessionId", out var sessionId)) await AddSessionTargetsAsync(sessionId, targets, cancellationToken);
        else if (TryGuid(root, "SubmissionId", out var submissionId))
        {
            var resolved = await db.Submissions.AsNoTracking().Where(x => x.Id == submissionId)
                .Select(x => (Guid?)x.SessionId).SingleOrDefaultAsync(cancellationToken);
            if (resolved is { } id) await AddSessionTargetsAsync(id, targets, cancellationToken);
        }
        if (TryGuid(root, "WorkerId", out var workerId)) targets.Add($"worker:{workerId:D}");
        return targets;
    }

    private async Task AddSessionTargetsAsync(Guid sessionId, HashSet<string> targets, CancellationToken ct)
    {
        var session = await db.ExamSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session is null) return;
        targets.Add($"exam:{session.ExamId:D}"); targets.Add($"chief:{session.ExamId:D}");
        targets.Add($"room:{session.RoomId:D}"); targets.Add($"candidate:{session.CandidateId:D}");
    }

    private static bool TryGuid(JsonElement element, string name, out Guid value)
    {
        value = default;
        if (!element.TryGetProperty(name, out var property)) return false;
        if (property.ValueKind == JsonValueKind.Object && property.TryGetProperty("Value", out var nested)) property = nested;
        return property.ValueKind == JsonValueKind.String && Guid.TryParse(property.GetString(), out value);
    }
}
