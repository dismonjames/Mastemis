using System.Text.Json;

namespace Mastemis.Infrastructure.Persistence.Problems;

internal static class ProblemOutbox
{
    public static OutboxRow Create(string type, Guid resourceId, DateTimeOffset now, object payload) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        ContractVersion = 1,
        ResourceId = resourceId.ToString("N"),
        Payload = JsonSerializer.Serialize(payload),
        OccurredAtUtc = now,
        CreatedAtUtc = now,
        NextAttemptAtUtc = now
    };
}
