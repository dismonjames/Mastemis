using System.Text.Json;
using Mastemis.Application;
using Mastemis.Application.Queries;
using Mastemis.Application.Workers.Queries;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Queries;

public sealed class PostgresWorkerInventoryQueryStore(MastemisDbContext db, IClock clock) : IWorkerInventoryQueryStore
{
    public async Task<PagedResult<WorkerInventoryItem>> ListAsync(WorkerListQuery request, CancellationToken cancellationToken)
    {
        var cutoff = clock.UtcNow.AddMinutes(-2);
        var query = db.JudgeWorkers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Name, $"%{term}%"));
        }
        if (string.Equals(request.Readiness, "ready", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.IsEnabled && x.LastHeartbeatUtc >= cutoff);
        else if (string.Equals(request.Readiness, "not-ready", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => !x.IsEnabled || x.LastHeartbeatUtc < cutoff || x.LastHeartbeatUtc == null);
        var total = await query.CountAsync(cancellationToken);
        var workers = await query.OrderByDescending(x => x.LastHeartbeatUtc).ThenBy(x => x.Name)
            .Skip(request.Offset).Take(request.Limit).ToArrayAsync(cancellationToken);
        var items = new List<WorkerInventoryItem>(workers.Length);
        foreach (var worker in workers)
        {
            var credential = await db.WorkerCredentials.AsNoTracking().Where(x => x.WorkerId == worker.Id)
                .OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.ExpiresAtUtc, x.RevokedAtUtc }).FirstOrDefaultAsync(cancellationToken);
            var normalJobs = await db.JudgeJobs.CountAsync(x => x.WorkerId == worker.Id &&
                (x.State == (int)JudgeJobState.Claimed || x.State == (int)JudgeJobState.Running), cancellationToken);
            var referenceJobs = await db.ReferenceOutputJobs.CountAsync(x => x.WorkerId == worker.Id &&
                (x.Status == 1 || x.Status == 2), cancellationToken);
            var lastNormalFailure = await db.JudgeJobs.AsNoTracking().Where(x => x.WorkerId == worker.Id && x.FailureCode != null)
                .OrderByDescending(x => x.CompletedAtUtc).Select(x => x.FailureCode).FirstOrDefaultAsync(cancellationToken);
            var lastReferenceFailure = await db.ReferenceOutputJobs.AsNoTracking().Where(x => x.WorkerId == worker.Id && x.FailureCode != null)
                .OrderByDescending(x => x.CompletedAtUtc).Select(x => x.FailureCode).FirstOrDefaultAsync(cancellationToken);
            var credentialStatus = credential is null ? "Missing" : credential.RevokedAtUtc is not null ? "Revoked"
                : credential.ExpiresAtUtc <= clock.UtcNow ? "Expired" : "Active";
            var used = normalJobs + referenceJobs;
            items.Add(new(worker.Id, worker.Name, worker.IsEnabled, credentialStatus, credential?.ExpiresAtUtc,
                worker.LastHeartbeatUtc, worker.IsEnabled && worker.LastHeartbeatUtc >= cutoff && credentialStatus == "Active",
                ParseLanguages(worker.LanguagesJson), worker.SandboxBackend, used, worker.Capacity, used,
                lastReferenceFailure ?? lastNormalFailure));
        }
        return new(items, request.Offset, request.Limit, total);
    }

    private static IReadOnlyList<string> ParseLanguages(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
