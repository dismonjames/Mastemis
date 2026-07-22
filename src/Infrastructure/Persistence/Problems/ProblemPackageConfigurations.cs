using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mastemis.Infrastructure.Persistence.Problems;

internal sealed class ProblemPackageImportConfiguration : IEntityTypeConfiguration<ProblemPackageImportRow>
{
    public void Configure(EntityTypeBuilder<ProblemPackageImportRow> b)
    {
        b.ToTable("problem_package_imports"); b.HasKey(x => x.Id); b.Property(x => x.PackageSha256).HasMaxLength(64);
        b.Property(x => x.IdempotencyKey).HasMaxLength(128); b.Property(x => x.Mode).HasMaxLength(32); b.HasIndex(x => x.PackageSha256);
        b.HasIndex(x => x.IdempotencyKey).IsUnique();
    }
}

internal sealed class ProblemPackageExportConfiguration : IEntityTypeConfiguration<ProblemPackageExportRow>
{
    public void Configure(EntityTypeBuilder<ProblemPackageExportRow> b)
    {
        b.ToTable("problem_package_exports"); b.HasKey(x => x.Id); b.Property(x => x.PackageSha256).HasMaxLength(64);
        b.Property(x => x.ObjectId).HasMaxLength(300); b.HasIndex(x => new { x.ProblemId, x.CreatedAtUtc });
    }
}
