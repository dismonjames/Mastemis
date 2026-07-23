using Mastemis.Contracts.Judge;
using Mastemis.Domain;

namespace Mastemis.Contracts.Problems.ReferenceValidation;

public sealed record ReferenceValidationSource(string LogicalFileName, string ObjectId, string Sha256, long Length);

public sealed record ReferenceValidationJobPayload(
    int ContractVersion,
    Guid ValidationId,
    ProblemId ProblemId,
    Guid ReferenceRevisionId,
    string Language,
    IReadOnlyList<ReferenceValidationSource> Sources,
    string CompilerProfile,
    UserId RequestedActor,
    DateTimeOffset RequestedAtUtc,
    TimeSpan CompilationTimeLimit)
{
    public const int CurrentVersion = 1;
    public const int MaximumSourceCount = 32;
    public const long MaximumSourceLength = 4_194_304;
    public const long MaximumTotalSourceLength = 16_777_216;

    public void Validate()
    {
        if (ContractVersion != CurrentVersion || ValidationId == Guid.Empty || ProblemId.Value == Guid.Empty ||
            ReferenceRevisionId == Guid.Empty || RequestedActor.Value == Guid.Empty || Language is not ("cpp" or "csharp") ||
            string.IsNullOrWhiteSpace(CompilerProfile) || CompilerProfile.Length > 128 ||
            CompilationTimeLimit <= TimeSpan.Zero || CompilationTimeLimit > TimeSpan.FromMinutes(5) ||
            Sources.Count is < 1 or > MaximumSourceCount || Sources.Sum(x => x.Length) > MaximumTotalSourceLength)
            throw Invalid();

        foreach (var source in Sources)
        {
            if (!ReferenceValidationContractRules.IsSafeLogicalFileName(source.LogicalFileName) ||
                string.IsNullOrWhiteSpace(source.ObjectId) || source.ObjectId.Length > 512 ||
                source.Sha256.Length != 64 || !source.Sha256.All(Uri.IsHexDigit) ||
                source.Length is < 1 or > MaximumSourceLength)
                throw Invalid();
        }

        if (Sources.Select(x => x.LogicalFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Sources.Count)
            throw Invalid();
    }

    private static JudgeContractException Invalid() =>
        new(JudgeFailureCode.InvalidContract, "Reference validation job contract is invalid.");
}

public sealed record ReferenceValidationLease(
    Guid ValidationId,
    Guid ReferenceRevisionId,
    JudgeWorkerId WorkerId,
    Guid LeaseToken,
    DateTimeOffset LeaseExpiresAtUtc,
    int Attempt,
    int MaximumAttempts);

public sealed record ReferenceValidationProgress(
    int ContractVersion,
    Guid ValidationId,
    JudgeWorkerId WorkerId,
    Guid LeaseToken,
    ReferenceValidationStatus Status,
    DateTimeOffset ReportedAtUtc);
