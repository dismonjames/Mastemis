using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mastemis.Infrastructure.Persistence.Problems;

internal sealed class ProblemAuthorAssignmentConfiguration : IEntityTypeConfiguration<ProblemAuthorAssignmentRow>
{
    public void Configure(EntityTypeBuilder<ProblemAuthorAssignmentRow> b)
    {
        b.ToTable("problem_author_assignments"); b.HasKey(x => new { x.ProblemId, x.UserId });
        b.HasIndex(x => new { x.UserId, x.Status, x.ExpiresAtUtc });
        b.HasOne<ProblemDraftRow>().WithMany().HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ExamProblemAssignmentConfiguration : IEntityTypeConfiguration<ExamProblemAssignmentRow>
{
    public void Configure(EntityTypeBuilder<ExamProblemAssignmentRow> b)
    {
        b.ToTable("exam_problem_assignments"); b.HasKey(x => new { x.ExamId, x.ProblemId }); b.HasIndex(x => x.ProblemId);
        b.HasOne<ExamRow>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProblemDraftRow>().WithMany().HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Restrict);
    }
}
