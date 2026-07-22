using Mastemis.Domain;

namespace Mastemis.Application.Problems.Statements;

public sealed record ProblemStatementContent(string Title, string Markdown, string InputDescription,
    string OutputDescription, string Constraints, string Notes);
public sealed record ProblemStatement(ProblemId ProblemId, string Locale, ProblemStatementContent Content,
    int Revision, string Sha256, long Length, UserId UpdatedBy, DateTimeOffset UpdatedAtUtc);
public sealed record ProblemStatementSummary(string Locale, string Title, int Revision, string Sha256,
    long Length, DateTimeOffset UpdatedAtUtc);

public interface IProblemStatementStore
{
    Task<IReadOnlyList<ProblemStatementSummary>> ListAsync(ProblemId problemId, CancellationToken cancellationToken);
    Task<ProblemStatement?> GetAsync(ProblemId problemId, string locale, CancellationToken cancellationToken);
    Task<ProblemStatement> SaveAsync(ProblemId problemId, string locale, ProblemStatementContent content,
        int? expectedRevision, CancellationToken cancellationToken);
    Task DeleteAsync(ProblemId problemId, string locale, CancellationToken cancellationToken);
}

public sealed class ProblemStatementService(IProblemStatementStore store, IAuthorizationService authorization)
{
    public async Task<IReadOnlyList<ProblemStatementSummary>> ListAsync(ProblemId id, CancellationToken ct)
    { await authorization.EnsureAsync("problem.read", id.Value, ct); return await store.ListAsync(id, ct); }
    public async Task<ProblemStatement> GetAsync(ProblemId id, string locale, CancellationToken ct)
    { await authorization.EnsureAsync("problem.read", id.Value, ct); return await store.GetAsync(id, locale, ct) ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem statement not found."); }
    public async Task<ProblemStatement> SaveAsync(ProblemId id, string locale, ProblemStatementContent content, int? revision, CancellationToken ct)
    { await authorization.EnsureAsync("problem.manage", id.Value, ct); Validate(content); return await store.SaveAsync(id, locale, content, revision, ct); }
    public async Task DeleteAsync(ProblemId id, string locale, CancellationToken ct)
    { await authorization.EnsureAsync("problem.manage", id.Value, ct); await store.DeleteAsync(id, locale, ct); }
    private static void Validate(ProblemStatementContent value)
    {
        if (string.IsNullOrWhiteSpace(value.Title) || value.Title.Length > 300 || value.Markdown.Length > 1_000_000 ||
            value.InputDescription.Length > 100_000 || value.OutputDescription.Length > 100_000 ||
            value.Constraints.Length > 100_000 || value.Notes.Length > 100_000)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Problem statement is invalid or too large.");
    }
}
