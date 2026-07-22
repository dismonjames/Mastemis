using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mastemis.Infrastructure.Persistence;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b) { b.Property(x => x.DisplayName).HasMaxLength(200); b.HasIndex(x => x.NormalizedUserName).IsUnique(); }
}
internal sealed class ExamConfiguration : IEntityTypeConfiguration<ExamRow>
{
    public void Configure(EntityTypeBuilder<ExamRow> b) { b.ToTable("exams"); b.HasKey(x => x.Id); b.Property(x => x.Title).HasMaxLength(300); b.HasIndex(x => new { x.State, x.StartsAtUtc }); }
}
internal sealed class RoomConfiguration : IEntityTypeConfiguration<RoomRow>
{
    public void Configure(EntityTypeBuilder<RoomRow> b) { b.ToTable("exam_rooms"); b.HasKey(x => x.Id); b.Property(x => x.Code).HasMaxLength(50); b.Property(x => x.Name).HasMaxLength(200); b.HasIndex(x => new { x.ExamId, x.Code }).IsUnique(); b.HasIndex(x => x.ExamId); b.HasOne<ExamRow>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class RoomAssignmentConfiguration : IEntityTypeConfiguration<RoomAssignmentRow>
{
    public void Configure(EntityTypeBuilder<RoomAssignmentRow> b) { b.ToTable("room_invigilator_assignments"); b.HasKey(x => new { x.RoomId, x.UserId }); b.HasIndex(x => x.UserId); }
}
internal sealed class ExamAssignmentConfiguration : IEntityTypeConfiguration<ExamAssignmentRow>
{
    public void Configure(EntityTypeBuilder<ExamAssignmentRow> b) { b.ToTable("exam_user_assignments"); b.HasKey(x => new { x.ExamId, x.UserId, x.Role }); b.Property(x => x.Role).HasMaxLength(50); b.HasIndex(x => new { x.UserId, x.ExamId }); }
}
internal sealed class CandidateConfiguration : IEntityTypeConfiguration<CandidateRow>
{
    public void Configure(EntityTypeBuilder<CandidateRow> b) { b.ToTable("candidates"); b.HasKey(x => x.Id); b.HasIndex(x => x.UserId).IsUnique(); }
}
internal sealed class CandidateRegistrationConfiguration : IEntityTypeConfiguration<CandidateRegistrationRow>
{
    public void Configure(EntityTypeBuilder<CandidateRegistrationRow> b) { b.ToTable("candidate_registrations"); b.HasKey(x => x.Id); b.Property(x => x.RegistrationCode).HasMaxLength(100); b.HasIndex(x => new { x.ExamId, x.RegistrationCode }).IsUnique(); b.HasIndex(x => new { x.ExamId, x.CandidateId }).IsUnique(); b.HasOne<ExamRow>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict); b.HasOne<CandidateRow>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class SessionConfiguration : IEntityTypeConfiguration<SessionRow>
{
    public void Configure(EntityTypeBuilder<SessionRow> b) { b.ToTable("exam_sessions"); b.HasKey(x => x.Id); b.Property(x => x.ConcurrencyToken).IsConcurrencyToken(); b.HasIndex(x => new { x.ExamId, x.State }); b.HasIndex(x => new { x.ExamId, x.CandidateId }).IsUnique().HasFilter("\"State\" IN (1, 2)"); b.HasIndex(x => new { x.RoomId, x.State }); b.HasIndex(x => new { x.CandidateId, x.State }); b.HasOne<ExamRow>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict); b.HasOne<RoomRow>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict); b.HasOne<CandidateRow>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class SourceRevisionConfiguration : IEntityTypeConfiguration<SourceRevisionRow>
{
    public void Configure(EntityTypeBuilder<SourceRevisionRow> b) { b.ToTable("source_revisions"); b.HasKey(x => x.Id); b.Property(x => x.ObjectId).HasMaxLength(300); b.Property(x => x.Sha256).HasMaxLength(64); b.HasIndex(x => new { x.SessionId, x.CreatedAtUtc }); b.HasOne<SessionRow>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class SubmissionConfiguration : IEntityTypeConfiguration<SubmissionRow>
{
    public void Configure(EntityTypeBuilder<SubmissionRow> b) { b.ToTable("submissions"); b.HasKey(x => x.Id); b.Property(x => x.Language).HasMaxLength(50); b.HasIndex(x => new { x.SessionId, x.CreatedAtUtc }); b.HasIndex(x => x.SessionId).IsUnique().HasFilter("\"IsFinal\" = TRUE"); b.HasOne<SessionRow>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict); b.HasOne<SourceRevisionRow>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class JudgementConfiguration : IEntityTypeConfiguration<JudgementRow>
{
    public void Configure(EntityTypeBuilder<JudgementRow> b) { b.ToTable("judgements"); b.HasKey(x => x.SubmissionId); b.Property(x => x.CompilerDiagnosticSummary).HasMaxLength(4096); b.Property(x => x.RuntimeDiagnosticSummary).HasMaxLength(1024); b.Property(x => x.CheckerDiagnosticSummary).HasMaxLength(1024); b.Property(x => x.SandboxBackend).HasMaxLength(100); b.Property(x => x.JudgeVersion).HasMaxLength(100); b.HasIndex(x => new { x.WorkerId, x.CompletedAtUtc }); b.HasOne<SubmissionRow>().WithOne().HasForeignKey<JudgementRow>(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class ProblemJudgeProfileConfiguration : IEntityTypeConfiguration<ProblemJudgeProfileRow>
{
    public void Configure(EntityTypeBuilder<ProblemJudgeProfileRow> b) { b.ToTable("problem_judge_profiles"); b.HasKey(x => x.ProblemId); }
}
internal sealed class ProblemTestCaseConfiguration : IEntityTypeConfiguration<ProblemTestCaseRow>
{
    public void Configure(EntityTypeBuilder<ProblemTestCaseRow> b) { b.ToTable("problem_test_cases"); b.HasKey(x => x.Id); b.Property(x => x.InputObjectId).HasMaxLength(300); b.Property(x => x.ExpectedObjectId).HasMaxLength(300); b.Property(x => x.CheckerId).HasMaxLength(32); b.HasIndex(x => new { x.ProblemId, x.TestIndex }).IsUnique(); }
}
internal sealed class SfeEventConfiguration : IEntityTypeConfiguration<SfeEventRow>
{
    public void Configure(EntityTypeBuilder<SfeEventRow> b) { b.ToTable("sfe_events"); b.HasKey(x => x.Id); b.Property(x => x.EventType).HasMaxLength(100); b.Property(x => x.MetadataJson).HasColumnType("jsonb"); b.HasIndex(x => new { x.SessionId, x.ClientSequence }).IsUnique(); b.HasIndex(x => new { x.SessionId, x.ServerReceivedAtUtc }); b.HasOne<SessionRow>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class SfeEvaluationConfiguration : IEntityTypeConfiguration<SfeEvaluationRow>
{
    public void Configure(EntityTypeBuilder<SfeEvaluationRow> b) { b.ToTable("sfe_evaluations"); b.HasKey(x => x.Id); b.Property(x => x.ReasonCode).HasMaxLength(100); b.Property(x => x.PolicyVersion).HasMaxLength(50); b.HasIndex(x => x.EventId).IsUnique(); b.HasIndex(x => new { x.SessionId, x.EvaluatedAtUtc }); b.HasOne<SfeEventRow>().WithOne().HasForeignKey<SfeEvaluationRow>(x => x.EventId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class WarningConfiguration : IEntityTypeConfiguration<WarningRow>
{
    public void Configure(EntityTypeBuilder<WarningRow> b) { b.ToTable("confirmed_warnings"); b.HasKey(x => x.Id); b.HasIndex(x => x.EvaluationId).IsUnique(); b.HasIndex(x => new { x.SessionId, x.Ordinal }).IsUnique(); b.HasIndex(x => new { x.ExamId, x.RoomId, x.CandidateId, x.IssuedAtUtc }); b.HasOne<SfeEvaluationRow>().WithOne().HasForeignKey<WarningRow>(x => x.EvaluationId).OnDelete(DeleteBehavior.Restrict); b.HasOne<SessionRow>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class WorkerConfiguration : IEntityTypeConfiguration<JudgeWorkerRow>
{
    public void Configure(EntityTypeBuilder<JudgeWorkerRow> b) { b.ToTable("judge_workers"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(200); b.Property(x => x.LanguagesJson).HasColumnType("jsonb"); b.Property(x => x.SandboxBackend).HasMaxLength(100); b.HasIndex(x => x.LastHeartbeatUtc); }
}
internal sealed class WorkerCredentialConfiguration : IEntityTypeConfiguration<WorkerCredentialRow>
{
    public void Configure(EntityTypeBuilder<WorkerCredentialRow> b) { b.ToTable("worker_credentials"); b.HasKey(x => x.Id); b.Property(x => x.SecretHash).HasMaxLength(1000); b.HasIndex(x => new { x.WorkerId, x.RevokedAtUtc }); b.HasOne<JudgeWorkerRow>().WithMany().HasForeignKey(x => x.WorkerId).OnDelete(DeleteBehavior.Cascade); }
}
internal sealed class JudgeJobConfiguration : IEntityTypeConfiguration<JudgeJobRow>
{
    public void Configure(EntityTypeBuilder<JudgeJobRow> b) { b.ToTable("judge_jobs"); b.HasKey(x => x.Id); b.Property(x => x.FailureCode).HasMaxLength(100); b.Property(x => x.ConcurrencyToken).IsConcurrencyToken(); b.HasIndex(x => x.SubmissionId).IsUnique(); b.HasIndex(x => new { x.State, x.Priority, x.AvailableAtUtc, x.CreatedAtUtc }); b.HasIndex(x => new { x.State, x.LeaseExpiresAtUtc }); b.HasOne<SubmissionRow>().WithOne().HasForeignKey<JudgeJobRow>(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class IdempotencyConfiguration : IEntityTypeConfiguration<IdempotencyRow>
{
    public void Configure(EntityTypeBuilder<IdempotencyRow> b) { b.ToTable("idempotency_records"); b.HasKey(x => new { x.Operation, x.Caller, x.Key }); b.Property(x => x.Operation).HasMaxLength(100); b.Property(x => x.Caller).HasMaxLength(100); b.Property(x => x.Key).HasMaxLength(128); }
}
internal sealed class OutboxConfiguration : IEntityTypeConfiguration<OutboxRow>
{
    public void Configure(EntityTypeBuilder<OutboxRow> b) { b.ToTable("outbox_messages"); b.HasKey(x => x.Id); b.Property(x => x.Type).HasMaxLength(300); b.Property(x => x.Payload).HasColumnType("jsonb"); b.Property(x => x.ResourceId).HasMaxLength(100); b.Property(x => x.FailureCode).HasMaxLength(100); b.HasIndex(x => new { x.ProcessedAtUtc, x.NextAttemptAtUtc, x.CreatedAtUtc }); }
}
internal sealed class AuditConfiguration : IEntityTypeConfiguration<AuditRow>
{
    public void Configure(EntityTypeBuilder<AuditRow> b) { b.ToTable("audit_records"); b.HasKey(x => x.Id); b.Property(x => x.Action).HasMaxLength(100); b.Property(x => x.ResourceType).HasMaxLength(100); b.Property(x => x.ResourceId).HasMaxLength(100); b.Property(x => x.MetadataJson).HasColumnType("jsonb"); b.HasIndex(x => new { x.ActorUserId, x.OccurredAtUtc }); b.HasIndex(x => new { x.ResourceType, x.ResourceId, x.OccurredAtUtc }); }
}
internal sealed class TerminationMetadataConfiguration : IEntityTypeConfiguration<TerminationMetadataRow>
{
    public void Configure(EntityTypeBuilder<TerminationMetadataRow> b) { b.ToTable("termination_metadata"); b.HasKey(x => x.SessionId); b.HasIndex(x => x.WarningId).IsUnique(); b.HasIndex(x => x.FinalSubmissionId).IsUnique(); b.HasIndex(x => x.JudgeJobId).IsUnique(); }
}
internal sealed class EvidencePackageConfiguration : IEntityTypeConfiguration<EvidencePackageRow>
{
    public void Configure(EntityTypeBuilder<EvidencePackageRow> b) { b.ToTable("evidence_packages"); b.HasKey(x => x.Id); b.Property(x => x.LatestChainHash).HasMaxLength(64); b.HasIndex(x => new { x.ExamId, x.RoomId, x.CandidateId, x.CreatedAtUtc }); b.HasIndex(x => x.SessionId).IsUnique(); }
}
internal sealed class EvidenceItemConfiguration : IEntityTypeConfiguration<EvidenceItemRow>
{
    public void Configure(EntityTypeBuilder<EvidenceItemRow> b) { b.ToTable("evidence_items"); b.HasKey(x => x.Id); b.Property(x => x.ContentType).HasMaxLength(200); b.Property(x => x.ObjectId).HasMaxLength(300); b.Property(x => x.ContentHash).HasMaxLength(64); b.Property(x => x.PreviousChainHash).HasMaxLength(64); b.Property(x => x.MetadataJson).HasColumnType("jsonb"); b.HasIndex(x => new { x.PackageId, x.ServerTimestampUtc }); b.HasOne<EvidencePackageRow>().WithMany().HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class EvidenceReviewGrantConfiguration : IEntityTypeConfiguration<EvidenceReviewGrantRow>
{
    public void Configure(EntityTypeBuilder<EvidenceReviewGrantRow> b) { b.ToTable("evidence_review_grants"); b.HasKey(x => new { x.PackageId, x.ReviewerId }); b.HasIndex(x => x.ReviewerId); b.HasOne<EvidencePackageRow>().WithMany().HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.Cascade); }
}
