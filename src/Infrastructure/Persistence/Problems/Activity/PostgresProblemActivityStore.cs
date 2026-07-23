using Mastemis.Application.Problems.Activity;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems.Activity;

public sealed class PostgresProblemActivityStore(MastemisDbContext db) : IProblemActivityStore
{
    public async Task<ProblemActivityPage> ListAsync(Guid problemId, ProblemActivityQuery query, CancellationToken ct)
    {
        var source = db.OutboxMessages.AsNoTracking().Where(x => x.ResourceId == problemId.ToString("N"));
        if (!string.IsNullOrWhiteSpace(query.Kind)) source = source.Where(x => x.Type == query.Kind);
        var rows = await source.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id)
            .Skip(query.Offset).Take(query.Limit + 1).Select(x => new { x.Id, x.Type, x.OccurredAtUtc }).ToArrayAsync(ct);
        var items = rows.Take(query.Limit).Select(x => new ProblemActivityEntry(x.Id, problemId, x.OccurredAtUtc,
            "Mastemis", x.Type, ProblemActivitySummary.Describe(x.Type), null, null)).ToArray();
        return new(items, query.Offset, query.Limit, rows.Length > query.Limit);
    }
}

internal static class ProblemActivitySummary
{
    public static string Describe(string kind) => kind switch
    {
        "ProblemDraftCreated" => "Draft created",
        "ProblemDraftUpdated" => "Draft metadata or reference solution updated",
        "ProblemStatementUpdated" => "Localized statement updated",
        "ProblemAssetUpdated" => "Problem asset changed",
        "MasSourceUpdated" => "MAS source updated",
        "MasValidationCompleted" => "MAS validation completed",
        "GenerationStarted" => "Test generation started",
        "GenerationProgressed" => "Test generation progressed",
        "GeneratedTestSetPublished" => "Generated test set published",
        "PackageImported" => "Package imported",
        "PackageExported" => "Package exported",
        "ProblemPermissionChanged" => "Problem permission changed",
        "ProblemExaminationAssignmentChanged" => "Examination assignment changed",
        _ => "Problem workflow state changed"
    };
}
