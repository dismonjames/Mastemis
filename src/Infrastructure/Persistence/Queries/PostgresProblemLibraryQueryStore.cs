using System.Text.Json;
using Mastemis.Application.Administration;
using Mastemis.Application.Problems.Authorization;
using Mastemis.Application.Problems.Queries;
using Mastemis.Application.Queries;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Queries;

public sealed class PostgresProblemLibraryQueryStore(MastemisDbContext db) : IProblemLibraryQueryStore
{
    public async Task<PagedResult<ProblemLibraryItem>> SearchAsync(UserId actorId, ProblemLibraryRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var assignments = db.ProblemAuthorAssignments.AsNoTracking().Where(x => x.UserId == actorId.Value &&
            x.Status == 0 && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > now));
        var managedExamIds = db.ExamAssignments.AsNoTracking().Where(x => x.UserId == actorId.Value).Select(x => x.ExamId);
        var managedProblemIds = db.ExamProblemAssignments.AsNoTracking().Where(x => managedExamIds.Contains(x.ExamId)).Select(x => x.ProblemId);
        var query = db.ProblemDrafts.AsNoTracking().Where(x => assignments.Select(a => a.ProblemId).Union(managedProblemIds).Contains(x.Id));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Title, $"%{search}%"));
        }
        if (!string.IsNullOrWhiteSpace(request.Difficulty)) query = query.Where(x => x.Difficulty == request.Difficulty);
        if (string.Equals(request.Status, "Published", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => db.GeneratedTestSets.Any(s => s.ProblemId == x.Id && s.Published));
        else if (string.Equals(request.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => !db.GeneratedTestSets.Any(s => s.ProblemId == x.Id && s.Published));
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.Title)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(x => new
            {
                x.Id, x.Title, x.Difficulty, x.TagsJson, x.AuthorsJson, x.UpdatedAtUtc,
                TestVersion = db.GeneratedTestSets.Where(s => s.ProblemId == x.Id && s.Published).Max(s => (int?)s.Version),
                AssignmentCount = db.ExamProblemAssignments.Count(a => a.ProblemId == x.Id),
                Role = assignments.Where(a => a.ProblemId == x.Id).Select(a => (int?)a.Role).FirstOrDefault(),
                Managed = managedProblemIds.Contains(x.Id)
            }).ToArrayAsync(cancellationToken);
        var items = rows.Select(x => new ProblemLibraryItem(x.Id, x.Title, x.TestVersion is null ? "Draft" : "Published",
            x.Difficulty, Parse(x.TagsJson), Parse(x.AuthorsJson), x.TestVersion, x.AssignmentCount,
            x.Role is { } role ? ((ProblemAuthorRole)role).ToString() : x.Managed ? "ExamManager" : "Viewer", x.UpdatedAtUtc)).ToArray();
        return new(items, request.Page, request.PageSize, count);
    }

    private static IReadOnlyList<string> Parse(string json) => JsonSerializer.Deserialize<string[]>(json) ?? [];
}
