using System.Text.Json;
using Mastemis.Application;
using Mastemis.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

public sealed class SignalROutboxPublisher(IHubContext<ExamHub> hub, IServiceScopeFactory scopeFactory, IClock clock)
    : IOutboxPublisher
{
    public async Task PublishAsync(string messageId, string messageType, string payload, CancellationToken cancellationToken)
    {
        var envelope = new RealtimeEnvelope(messageId, 1, messageType, clock.UtcNow, payload);
        var targets = new HashSet<string>(StringComparer.Ordinal);
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (TryGuid(root, "SessionId", out var sessionId))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MastemisDbContext>();
            var session = await db.ExamSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
            if (session is not null)
            {
                targets.Add($"exam:{session.ExamId:D}"); targets.Add($"chief:{session.ExamId:D}");
                targets.Add($"room:{session.RoomId:D}"); targets.Add($"candidate:{session.CandidateId:D}");
            }
        }
        if (TryGuid(root, "WorkerId", out var workerId)) targets.Add($"worker:{workerId:D}");
        foreach (var target in targets) await hub.Clients.Group(target).SendAsync("mastemis.event", envelope, cancellationToken);
    }

    private static bool TryGuid(JsonElement element, string name, out Guid value)
    {
        value = default;
        if (!element.TryGetProperty(name, out var property)) return false;
        if (property.ValueKind == JsonValueKind.Object && property.TryGetProperty("Value", out var nested)) property = nested;
        return property.ValueKind == JsonValueKind.String && Guid.TryParse(property.GetString(), out value);
    }
}
