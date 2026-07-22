using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mastemis.Infrastructure.Persistence.Problems;

internal sealed class ProblemDraftConfiguration : IEntityTypeConfiguration<ProblemDraftRow>
{
    public void Configure(EntityTypeBuilder<ProblemDraftRow> b)
    {
        b.ToTable("problem_drafts"); b.HasKey(x => x.Id); b.Property(x => x.Title).HasMaxLength(300);
        b.Property(x => x.AuthorsJson).HasColumnType("jsonb"); b.Property(x => x.TagsJson).HasColumnType("jsonb");
        b.Property(x => x.Difficulty).HasMaxLength(32); b.Property(x => x.AcceptedLanguagesJson).HasColumnType("jsonb");
        b.Property(x => x.DefaultLocale).HasMaxLength(16); b.Property(x => x.Checker).HasMaxLength(32);
        b.Property(x => x.MasSource).HasColumnType("text"); b.Property(x => x.MasSha256).HasMaxLength(64);
        b.Property(x => x.MasValidationJson).HasColumnType("jsonb"); b.Property(x => x.MasRuntimeVersion).HasMaxLength(100);
        b.Property(x => x.ConcurrencyToken).IsConcurrencyToken(); b.HasIndex(x => x.UpdatedAtUtc);
    }
}

internal sealed class ProblemStatementConfiguration : IEntityTypeConfiguration<ProblemStatementRow>
{
    public void Configure(EntityTypeBuilder<ProblemStatementRow> b)
    {
        b.ToTable("problem_statements"); b.HasKey(x => new { x.ProblemId, x.Locale }); b.Property(x => x.Locale).HasMaxLength(16);
        b.Property(x => x.Title).HasMaxLength(300);
        b.Property(x => x.ObjectId).HasMaxLength(300); b.Property(x => x.Sha256).HasMaxLength(64); b.HasIndex(x => new { x.ProblemId, x.UpdatedAtUtc });
        b.HasOne<ProblemDraftRow>().WithMany().HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Cascade);
    }
}
