namespace Mastemis.Server.Endpoints.ProblemStudio;

using Mastemis.Application.Problems.Authorization;
using Mastemis.Application.Problems.Drafts;
using Mastemis.Application.Problems.Statements;

public sealed record CreateProblemDraftRequest(string Title, string DefaultLocale);
public sealed record UpdateMasSourceRequest(string Source, int ExpectedRevision);
public sealed record ValidateMasSourceRequest(string Source);
public sealed record PreviewMasRequest(string Source, ulong Seed, int MaximumTests = 5);
public sealed record StartGenerationRequest(ulong Seed);
public sealed record AssignProblemAuthorRequest(ProblemAuthorRole Role, DateTimeOffset? ExpiresAtUtc);
public sealed record UpdateProblemStatementRequest(ProblemStatementContent Content, int? ExpectedRevision);
public sealed record ReferenceSolutionSourceRequest(string FileName, string ContentBase64);
public sealed record UpdateReferenceSolutionRequest(string Language, IReadOnlyList<ReferenceSolutionSourceRequest> Sources);
public sealed record UpdateProblemDraftRequest(string Title, IReadOnlyList<string> Authors, IReadOnlyList<string> Tags,
    string Difficulty, string DefaultLocale, IReadOnlyList<string> AcceptedLanguages, long TimeLimitMilliseconds,
    long MemoryLimitBytes, long OutputLimitBytes, string Checker, int ExpectedVersion)
{
    public ProblemDraftUpdate ToCommand() => new(Title, Authors, Tags, Difficulty, DefaultLocale, AcceptedLanguages,
        TimeLimitMilliseconds, MemoryLimitBytes, OutputLimitBytes, Checker, ExpectedVersion);
}

public sealed record ProblemDraftResponse(Guid Id, string Title, string DefaultLocale, long TimeLimitMilliseconds,
    long MemoryLimitBytes, long OutputLimitBytes, string Checker, string MasSha256);
