using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mastemis.Infrastructure.Persistence.Problems;

internal sealed class ProblemAssetConfiguration : IEntityTypeConfiguration<ProblemAssetRow>
{
    public void Configure(EntityTypeBuilder<ProblemAssetRow> b)
    {
        b.ToTable("problem_assets"); b.HasKey(x => x.Id); b.Property(x => x.LogicalName).HasMaxLength(200);
        b.Property(x => x.NormalizedName).HasMaxLength(200); b.Property(x => x.ContentType).HasMaxLength(100);
        b.Property(x => x.ObjectId).HasMaxLength(300); b.Property(x => x.Sha256).HasMaxLength(64);
        b.HasIndex(x => new { x.ProblemId, x.NormalizedName }).IsUnique(); b.HasIndex(x => x.ObjectId).IsUnique();
        b.HasOne<ProblemDraftRow>().WithMany().HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Restrict);
    }
}
