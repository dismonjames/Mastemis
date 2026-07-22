using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mastemis.Infrastructure.Persistence.Problems;

internal sealed class ReferenceOutputJobConfiguration : IEntityTypeConfiguration<ReferenceOutputJobRow>
{
    public void Configure(EntityTypeBuilder<ReferenceOutputJobRow> b)
    {
        b.ToTable("reference_output_jobs"); b.HasKey(x => x.Id); b.Property(x => x.Language).HasMaxLength(32);
        b.Property(x => x.PayloadJson).HasColumnType("jsonb"); b.Property(x => x.FailureCode).HasMaxLength(100);
        b.Property(x => x.ConcurrencyToken).IsConcurrencyToken(); b.HasIndex(x => x.OperationId).IsUnique();
        b.HasIndex(x => new { x.Status, x.AvailableAtUtc, x.CreatedAtUtc }); b.HasIndex(x => new { x.Status, x.LeaseExpiresAtUtc });
        b.HasOne<ProblemGenerationOperationRow>().WithOne().HasForeignKey<ReferenceOutputJobRow>(x => x.OperationId).OnDelete(DeleteBehavior.Restrict);
    }
}
