using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mastemis.Infrastructure.Persistence.Problems;

internal sealed class ReferenceSolutionRevisionConfiguration : IEntityTypeConfiguration<ReferenceSolutionRevisionRow>
{
    public void Configure(EntityTypeBuilder<ReferenceSolutionRevisionRow> b)
    {
        b.ToTable("reference_solution_revisions"); b.HasKey(x => x.Id); b.Property(x => x.Language).HasMaxLength(32);
        b.Property(x => x.CompileProfile).HasMaxLength(100); b.HasIndex(x => x.ProblemId).IsUnique().HasFilter("\"IsCurrent\" = TRUE");
        b.HasIndex(x => new { x.ProblemId, x.CreatedAtUtc }); b.HasOne<ProblemDraftRow>().WithMany().HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class ReferenceSolutionSourceConfiguration : IEntityTypeConfiguration<ReferenceSolutionSourceRow>
{
    public void Configure(EntityTypeBuilder<ReferenceSolutionSourceRow> b)
    {
        b.ToTable("reference_solution_sources"); b.HasKey(x => new { x.RevisionId, x.FileName }); b.Property(x => x.FileName).HasMaxLength(100);
        b.Property(x => x.ObjectId).HasMaxLength(300); b.Property(x => x.Sha256).HasMaxLength(64); b.HasIndex(x => x.ObjectId).IsUnique();
        b.HasOne<ReferenceSolutionRevisionRow>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
    }
}
