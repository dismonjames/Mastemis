using Mastemis.Infrastructure.Persistence.Problems;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence;

public sealed class MastemisDbContext(DbContextOptions<MastemisDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<ExamRow> Exams => Set<ExamRow>();
    public DbSet<RoomRow> Rooms => Set<RoomRow>();
    public DbSet<RoomAssignmentRow> RoomAssignments => Set<RoomAssignmentRow>();
    public DbSet<ExamAssignmentRow> ExamAssignments => Set<ExamAssignmentRow>();
    public DbSet<CandidateRow> Candidates => Set<CandidateRow>();
    public DbSet<CandidateRegistrationRow> CandidateRegistrations => Set<CandidateRegistrationRow>();
    public DbSet<SessionRow> ExamSessions => Set<SessionRow>();
    public DbSet<SourceRevisionRow> SourceRevisions => Set<SourceRevisionRow>();
    public DbSet<SubmissionRow> Submissions => Set<SubmissionRow>();
    public DbSet<JudgementRow> Judgements => Set<JudgementRow>();
    public DbSet<ProblemJudgeProfileRow> ProblemJudgeProfiles => Set<ProblemJudgeProfileRow>();
    public DbSet<ProblemTestCaseRow> ProblemTestCases => Set<ProblemTestCaseRow>();
    public DbSet<SfeEventRow> SfeEvents => Set<SfeEventRow>();
    public DbSet<SfeEvaluationRow> SfeEvaluations => Set<SfeEvaluationRow>();
    public DbSet<WarningRow> ConfirmedWarnings => Set<WarningRow>();
    public DbSet<JudgeWorkerRow> JudgeWorkers => Set<JudgeWorkerRow>();
    public DbSet<WorkerCredentialRow> WorkerCredentials => Set<WorkerCredentialRow>();
    public DbSet<JudgeJobRow> JudgeJobs => Set<JudgeJobRow>();
    public DbSet<IdempotencyRow> IdempotencyRecords => Set<IdempotencyRow>();
    public DbSet<OutboxRow> OutboxMessages => Set<OutboxRow>();
    public DbSet<AuditRow> AuditRecords => Set<AuditRow>();
    public DbSet<TerminationMetadataRow> TerminationMetadata => Set<TerminationMetadataRow>();
    public DbSet<EvidencePackageRow> EvidencePackages => Set<EvidencePackageRow>();
    public DbSet<EvidenceItemRow> EvidenceItems => Set<EvidenceItemRow>();
    public DbSet<EvidenceReviewGrantRow> EvidenceReviewGrants => Set<EvidenceReviewGrantRow>();
    public DbSet<ProblemDraftRow> ProblemDrafts => Set<ProblemDraftRow>();
    public DbSet<ProblemStatementRow> ProblemStatements => Set<ProblemStatementRow>();
    public DbSet<ProblemGenerationOperationRow> ProblemGenerationOperations => Set<ProblemGenerationOperationRow>();
    public DbSet<GeneratedTestSetRow> GeneratedTestSets => Set<GeneratedTestSetRow>();
    public DbSet<GeneratedTestRow> GeneratedTests => Set<GeneratedTestRow>();
    public DbSet<ProblemPackageImportRow> ProblemPackageImports => Set<ProblemPackageImportRow>();
    public DbSet<ProblemPackageExportRow> ProblemPackageExports => Set<ProblemPackageExportRow>();
    public DbSet<ProblemAuthorAssignmentRow> ProblemAuthorAssignments => Set<ProblemAuthorAssignmentRow>();
    public DbSet<ExamProblemAssignmentRow> ExamProblemAssignments => Set<ExamProblemAssignmentRow>();
    public DbSet<ProblemAssetRow> ProblemAssets => Set<ProblemAssetRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MastemisDbContext).Assembly);
    }
}

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public bool MustChangePassword { get; set; }
}

public sealed class ExamRow
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int State { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
}
public sealed class RoomRow { public Guid Id { get; set; } public Guid ExamId { get; set; } public string Code { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; }
public sealed class RoomAssignmentRow { public Guid RoomId { get; set; } public Guid UserId { get; set; } public DateTimeOffset AssignedAtUtc { get; set; } }
public sealed class ExamAssignmentRow { public Guid ExamId { get; set; } public Guid UserId { get; set; } public string Role { get; set; } = string.Empty; public DateTimeOffset AssignedAtUtc { get; set; } }
public sealed class CandidateRow { public Guid Id { get; set; } public Guid UserId { get; set; } }
public sealed class CandidateRegistrationRow { public Guid Id { get; set; } public Guid ExamId { get; set; } public Guid CandidateId { get; set; } public string RegistrationCode { get; set; } = string.Empty; public int AccessState { get; set; } public DateTimeOffset CreatedAtUtc { get; set; } }
public sealed class SessionRow
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid RoomId { get; set; }
    public Guid CandidateId { get; set; }
    public int State { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? TerminatedAtUtc { get; set; }
    public Guid? CurrentRevisionId { get; set; }
    public Guid? FrozenRevisionId { get; set; }
    public int Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
}
public sealed class SourceRevisionRow { public Guid Id { get; set; } public Guid SessionId { get; set; } public string ObjectId { get; set; } = string.Empty; public string Sha256 { get; set; } = string.Empty; public long Length { get; set; } public DateTimeOffset CreatedAtUtc { get; set; } }
public sealed class SubmissionRow { public Guid Id { get; set; } public Guid SessionId { get; set; } public Guid ProblemId { get; set; } public Guid RevisionId { get; set; } public string Language { get; set; } = string.Empty; public int State { get; set; } public bool IsFinal { get; set; } public DateTimeOffset CreatedAtUtc { get; set; } }
public sealed class JudgementRow
{
    public Guid SubmissionId { get; set; }
    public int Verdict { get; set; }
    public int Score { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public int? FailedTestIndex { get; set; }
    public long ExecutionMilliseconds { get; set; }
    public long? PeakMemoryBytes { get; set; }
    public int? ExitCode { get; set; }
    public int? Signal { get; set; }
    public long StandardOutputBytes { get; set; }
    public long StandardErrorBytes { get; set; }
    public string? CompilerDiagnosticSummary { get; set; }
    public string? RuntimeDiagnosticSummary { get; set; }
    public string? CheckerDiagnosticSummary { get; set; }
    public string SandboxBackend { get; set; } = string.Empty;
    public Guid? WorkerId { get; set; }
    public string JudgeVersion { get; set; } = string.Empty;
}
public sealed class ProblemJudgeProfileRow
{
    public Guid ProblemId { get; set; }
    public long CpuMilliseconds { get; set; }
    public long WallMilliseconds { get; set; }
    public long MemoryBytes { get; set; }
    public long OutputBytes { get; set; }
    public long FileBytes { get; set; }
    public int ProcessCount { get; set; }
    public int TestCount { get; set; }
    public long CompilationMilliseconds { get; set; }
    public long CompilationOutputBytes { get; set; }
}
public sealed class ProblemTestCaseRow
{
    public Guid Id { get; set; }
    public Guid ProblemId { get; set; }
    public int TestIndex { get; set; }
    public string InputObjectId { get; set; } = string.Empty;
    public string ExpectedObjectId { get; set; } = string.Empty;
    public long InputBytes { get; set; }
    public long ExpectedBytes { get; set; }
    public string CheckerId { get; set; } = "exact";
}
public sealed class SfeEventRow { public Guid Id { get; set; } public Guid SessionId { get; set; } public long ClientSequence { get; set; } public DateTimeOffset ClientTimestamp { get; set; } public DateTimeOffset ServerReceivedAtUtc { get; set; } public string EventType { get; set; } = string.Empty; public string MetadataJson { get; set; } = "{}"; }
public sealed class SfeEvaluationRow { public Guid Id { get; set; } public Guid EventId { get; set; } public Guid SessionId { get; set; } public int Result { get; set; } public string ReasonCode { get; set; } = string.Empty; public string PolicyVersion { get; set; } = "baseline.v1"; public DateTimeOffset EvaluatedAtUtc { get; set; } }
public sealed class WarningRow { public Guid Id { get; set; } public Guid ExamId { get; set; } public Guid RoomId { get; set; } public Guid CandidateId { get; set; } public Guid SessionId { get; set; } public Guid EvaluationId { get; set; } public int Ordinal { get; set; } public DateTimeOffset IssuedAtUtc { get; set; } }
public sealed class JudgeWorkerRow { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; public int Capacity { get; set; } public bool IsEnabled { get; set; } public DateTimeOffset? LastHeartbeatUtc { get; set; } public DateTimeOffset CreatedAtUtc { get; set; } public string LanguagesJson { get; set; } = "[]"; public string? SandboxBackend { get; set; } }
public sealed class WorkerCredentialRow { public Guid Id { get; set; } public Guid WorkerId { get; set; } public string SecretHash { get; set; } = string.Empty; public DateTimeOffset CreatedAtUtc { get; set; } public DateTimeOffset? ExpiresAtUtc { get; set; } public DateTimeOffset? RevokedAtUtc { get; set; } }
public sealed class JudgeJobRow
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public int State { get; set; }
    public int Priority { get; set; }
    public int Attempt { get; set; }
    public int MaximumAttempts { get; set; } = 3;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset AvailableAtUtc { get; set; }
    public Guid? WorkerId { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? FailureCode { get; set; }
    public Guid ConcurrencyToken { get; set; }
}
public sealed class IdempotencyRow { public string Operation { get; set; } = string.Empty; public string Caller { get; set; } = string.Empty; public string Key { get; set; } = string.Empty; public DateTimeOffset CreatedAtUtc { get; set; } }
public sealed class OutboxRow { public Guid Id { get; set; } public string Type { get; set; } = string.Empty; public int ContractVersion { get; set; } = 1; public string Payload { get; set; } = string.Empty; public string? ResourceId { get; set; } public DateTimeOffset OccurredAtUtc { get; set; } public DateTimeOffset CreatedAtUtc { get; set; } public DateTimeOffset NextAttemptAtUtc { get; set; } public DateTimeOffset? ProcessedAtUtc { get; set; } public int Attempts { get; set; } public string? FailureCode { get; set; } }
public sealed class AuditRow { public Guid Id { get; set; } public Guid? ActorUserId { get; set; } public Guid? WorkerId { get; set; } public string Action { get; set; } = string.Empty; public string ResourceType { get; set; } = string.Empty; public string ResourceId { get; set; } = string.Empty; public DateTimeOffset OccurredAtUtc { get; set; } public string MetadataJson { get; set; } = "{}"; }
public sealed class TerminationMetadataRow { public Guid SessionId { get; set; } public Guid WarningId { get; set; } public Guid FrozenRevisionId { get; set; } public Guid FinalSubmissionId { get; set; } public Guid JudgeJobId { get; set; } public DateTimeOffset CreatedAtUtc { get; set; } }
public sealed class EvidencePackageRow { public Guid Id { get; set; } public Guid ExamId { get; set; } public Guid RoomId { get; set; } public Guid CandidateId { get; set; } public Guid SessionId { get; set; } public DateTimeOffset CreatedAtUtc { get; set; } public string? LatestChainHash { get; set; } }
public sealed class EvidenceItemRow { public Guid Id { get; set; } public Guid PackageId { get; set; } public int Type { get; set; } public DateTimeOffset ServerTimestampUtc { get; set; } public string ContentType { get; set; } = string.Empty; public string? ObjectId { get; set; } public string ContentHash { get; set; } = string.Empty; public string? PreviousChainHash { get; set; } public string MetadataJson { get; set; } = "{}"; }
public sealed class EvidenceReviewGrantRow { public Guid PackageId { get; set; } public Guid ReviewerId { get; set; } public Guid GrantedByUserId { get; set; } public DateTimeOffset GrantedAtUtc { get; set; } }
