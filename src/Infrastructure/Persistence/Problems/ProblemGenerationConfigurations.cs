using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mastemis.Infrastructure.Persistence.Problems;

internal sealed class ProblemGenerationOperationConfiguration : IEntityTypeConfiguration<ProblemGenerationOperationRow>
{
    public void Configure(EntityTypeBuilder<ProblemGenerationOperationRow> b)
    {
        b.ToTable("problem_generation_operations"); b.HasKey(x => x.Id); b.Property(x => x.RuntimeVersion).HasMaxLength(64);
        b.Property(x => x.PrngAlgorithm).HasMaxLength(64); b.Property(x => x.FailureCode).HasMaxLength(100); b.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        b.HasIndex(x => new { x.ProblemId, x.Status }); b.HasIndex(x => x.ProblemId).IsUnique().HasFilter("\"Status\" IN (0, 1)");
        b.HasOne<ProblemDraftRow>().WithMany().HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GeneratedTestSetConfiguration : IEntityTypeConfiguration<GeneratedTestSetRow>
{
    public void Configure(EntityTypeBuilder<GeneratedTestSetRow> b)
    {
        b.ToTable("generated_test_sets"); b.HasKey(x => x.Id); b.HasIndex(x => x.GenerationOperationId).IsUnique();
        b.HasIndex(x => new { x.ProblemId, x.Version }).IsUnique(); b.HasIndex(x => new { x.ProblemId, x.Published });
        b.HasOne<ProblemDraftRow>().WithMany().HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GeneratedTestConfiguration : IEntityTypeConfiguration<GeneratedTestRow>
{
    public void Configure(EntityTypeBuilder<GeneratedTestRow> b)
    {
        b.ToTable("generated_tests"); b.HasKey(x => x.Id); b.Property(x => x.Group).HasMaxLength(100); b.Property(x => x.Visibility).HasMaxLength(20);
        b.Property(x => x.Checker).HasMaxLength(32); b.Property(x => x.InputObjectId).HasMaxLength(300); b.Property(x => x.OutputObjectId).HasMaxLength(300);
        b.Property(x => x.InputSha256).HasMaxLength(64); b.Property(x => x.OutputSha256).HasMaxLength(64); b.HasIndex(x => new { x.TestSetId, x.TestIndex }).IsUnique();
        b.HasOne<GeneratedTestSetRow>().WithMany().HasForeignKey(x => x.TestSetId).OnDelete(DeleteBehavior.Cascade);
    }
}
