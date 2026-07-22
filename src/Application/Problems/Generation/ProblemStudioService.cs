using System.Security.Cryptography;
using System.Text;
using Mastemis.Application.Problems.Authoring;
using Mastemis.Domain;
using Mastemis.Mas.Language.Semantics;
using Mastemis.Mas.Language.Syntax;
using Mastemis.Mas.Runtime.Execution;
using Mastemis.Mas.Runtime.Generation;
using Mastemis.Mas.Runtime.Limits;

namespace Mastemis.Application.Problems.Generation;

public sealed record MasValidationResult(bool Valid, IReadOnlyList<Mastemis.Mas.Language.Diagnostics.MasDiagnostic> Diagnostics, string SourceHash);
public sealed record MasPreviewResult(bool Valid, ulong Seed, string RuntimeVersion, IReadOnlyList<GeneratedTest> Tests,
    IReadOnlyList<Mastemis.Mas.Language.Diagnostics.MasDiagnostic> Diagnostics, bool Truncated);

public sealed class ProblemStudioService(IProblemStudioStore store, IAuthorizationService authorization)
{
    public async Task<DraftProblem> CreateAsync(string title, string locale, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("problem.create", Guid.Empty, cancellationToken);
        if (string.IsNullOrWhiteSpace(title) || title.Length > 300 || locale.Length is < 2 or > 16) throw Invalid();
        return await store.CreateAsync(title.Trim(), locale, cancellationToken);
    }
    public async Task<MasValidationResult> ValidateAsync(ProblemId problemId, string source, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("problem.manage", problemId.Value, cancellationToken); cancellationToken.ThrowIfCancellationRequested();
        var tree = SyntaxTree.Parse(source); var model = new MasSemanticValidator().Validate(tree);
        return new(!model.Diagnostics.Any(x => x.Severity == Mastemis.Mas.Language.Diagnostics.MasDiagnosticSeverity.Error),
            model.Diagnostics, Hash(source));
    }
    public async Task SaveMasAsync(ProblemId problemId, string source, CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(problemId, source, cancellationToken);
        if (!validation.Valid) throw new ApplicationFailure(ErrorCodes.InvalidInput, "MAS source contains errors.");
        await store.SaveMasAsync(problemId, source, validation.SourceHash, cancellationToken);
    }
    public async Task<MasPreviewResult> PreviewAsync(ProblemId problemId, string source, ulong seed, int maximumTests,
        CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("problem.manage", problemId.Value, cancellationToken); var bounded = Math.Clamp(maximumTests, 1, 10);
        var report = new MasRuntime(new(MaximumTests: 10_000, MaximumOutputBytes: 1024 * 1024, MaximumDuration: TimeSpan.FromSeconds(2)))
            .Generate(source, seed, cancellationToken);
        var tests = report.Tests.Take(bounded).Select(x => x with { Input = x.Input.Length <= 64 * 1024 ? x.Input : x.Input[..(64 * 1024)] }).ToArray();
        return new(report.Diagnostics.All(x => x.Severity != Mastemis.Mas.Language.Diagnostics.MasDiagnosticSeverity.Error), seed,
            report.RuntimeVersion, tests, report.Diagnostics, report.Tests.Count > bounded || tests.Any(x => x.Input.Length == 64 * 1024));
    }
    public async Task<ProblemGenerationOperation> GenerateAsync(ProblemId problemId, ulong seed, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("problem.manage", problemId.Value, cancellationToken);
        var problem = await store.GetAsync(problemId, cancellationToken) ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem not found.");
        var operation = await store.BeginGenerationAsync(problemId, seed, MasRuntime.RuntimeVersion, cancellationToken);
        try
        {
            operation = await store.TransitionGenerationAsync(operation.Id, GenerationOperationStatus.Validating, 0, 1, cancellationToken);
            var report = new MasRuntime(new()).Generate(problem.MasSource, seed, cancellationToken);
            if (report.Diagnostics.Any(x => x.Severity == Mastemis.Mas.Language.Diagnostics.MasDiagnosticSeverity.Error))
                throw new ApplicationFailure(ErrorCodes.InvalidInput, "MAS source contains errors.");
            operation = await store.TransitionGenerationAsync(operation.Id, GenerationOperationStatus.GeneratingInputs, 0, report.Tests.Count, cancellationToken);
            await store.StageInputsAsync(operation, report.Tests.Select(x => (x.Index, x.Group, Encoding.UTF8.GetBytes(x.Input), x.Sha256)).ToArray(), cancellationToken);
            return (await store.GetGenerationAsync(operation.Id, cancellationToken))!;
        }
        catch (OperationCanceledException) { await store.CancelGenerationAsync(operation.Id, CancellationToken.None); throw; }
        catch (Exception exception) when (exception is MasRuntimeException or ApplicationFailure)
        { await store.FailGenerationAsync(operation.Id, exception is MasRuntimeException runtime ? runtime.Code : ErrorCodes.InvalidInput, cancellationToken); throw; }
    }
    public async Task CancelAsync(Guid operationId, ProblemId problemId, CancellationToken cancellationToken)
    { await authorization.EnsureAsync("problem.manage", problemId.Value, cancellationToken); await store.CancelGenerationAsync(operationId, cancellationToken); }
    public async Task<ProblemGenerationOperation?> GetStatusAsync(Guid operationId, ProblemId problemId, CancellationToken cancellationToken)
    { await authorization.EnsureAsync("problem.manage", problemId.Value, cancellationToken); return await store.GetGenerationAsync(operationId, cancellationToken); }
    private static string Hash(string source) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    private static ApplicationFailure Invalid() => new(ErrorCodes.InvalidInput, "Problem metadata is invalid.");
}
