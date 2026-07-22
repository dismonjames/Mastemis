using Mastemis.Domain;
using Mastemis.Mas.Language.Diagnostics;
using Mastemis.Mas.Language.Semantics;
using Mastemis.Mas.Language.Syntax;
using Mastemis.Mas.Runtime.Execution;

namespace Mastemis.Application.Problems.Mas;

public sealed record ProblemMasSource(ProblemId ProblemId, string Source, string Sha256, int Revision,
    string RuntimeVersion, DateTimeOffset? ValidatedAtUtc, IReadOnlyList<MasDiagnostic> LatestDiagnostics);

public interface IProblemMasStore
{
    Task<ProblemMasSource?> GetAsync(ProblemId problemId, CancellationToken cancellationToken);
    Task<ProblemMasSource> SaveAsync(ProblemId problemId, string source, string sha256, int expectedRevision,
        IReadOnlyList<MasDiagnostic> diagnostics, CancellationToken cancellationToken);
    Task SaveValidationAsync(ProblemId problemId, string sha256, IReadOnlyList<MasDiagnostic> diagnostics,
        CancellationToken cancellationToken);
}

public sealed class ProblemMasService(IProblemMasStore store, IAuthorizationService authorization)
{
    public async Task<ProblemMasSource> GetAsync(ProblemId problemId, CancellationToken cancellationToken)
    { await authorization.EnsureAsync("problem.read", problemId.Value, cancellationToken); return await store.GetAsync(problemId, cancellationToken) ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem MAS source not found."); }

    public async Task<ProblemMasSource> SaveAsync(ProblemId problemId, string source, int expectedRevision, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("problem.manage", problemId.Value, cancellationToken); ValidateSize(source);
        var result = Validate(source); if (result.Diagnostics.Any(x => x.Severity == MasDiagnosticSeverity.Error))
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "MAS source contains errors.");
        return await store.SaveAsync(problemId, source, result.Hash, expectedRevision, result.Diagnostics, cancellationToken);
    }

    public async Task<(bool Valid, string Hash, IReadOnlyList<MasDiagnostic> Diagnostics)> ValidateAsync(ProblemId problemId,
        string source, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("problem.manage", problemId.Value, cancellationToken); ValidateSize(source);
        var result = Validate(source); await store.SaveValidationAsync(problemId, result.Hash, result.Diagnostics, cancellationToken);
        return (!result.Diagnostics.Any(x => x.Severity == MasDiagnosticSeverity.Error), result.Hash, result.Diagnostics);
    }

    private static (string Hash, IReadOnlyList<MasDiagnostic> Diagnostics) Validate(string source)
    { var tree = SyntaxTree.Parse(source); var model = new MasSemanticValidator().Validate(tree); return (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source))).ToLowerInvariant(), model.Diagnostics); }
    private static void ValidateSize(string source) { if (source.Length > 1_048_576) throw new ApplicationFailure(ErrorCodes.InvalidInput, "MAS source exceeds its size limit."); }
}
