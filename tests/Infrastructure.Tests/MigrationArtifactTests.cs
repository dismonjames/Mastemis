using Mastemis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mastemis.Infrastructure.Tests;

public sealed class MigrationArtifactTests
{
    [Fact]
    public void Initial_production_migration_generates_complete_postgresql_script()
    {
        using var db = new MastemisDbContext(new DbContextOptionsBuilder<MastemisDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options);
        var migrations = db.Database.GetMigrations().ToArray();
        Assert.Equal(9, migrations.Length);
        Assert.EndsWith("_InitialProduction", migrations[0], StringComparison.Ordinal);
        Assert.EndsWith("_AddHumanAdministration", migrations[1], StringComparison.Ordinal);
        Assert.EndsWith("_AddEvidenceMetadata", migrations[2], StringComparison.Ordinal);
        Assert.EndsWith("_AddJudgeExecutionReporting", migrations[3], StringComparison.Ordinal);
        Assert.EndsWith("_AddProblemAuthoring", migrations[4], StringComparison.Ordinal);
        Assert.EndsWith("_AddProblemAuthorScopes", migrations[5], StringComparison.Ordinal);
        Assert.EndsWith("_ExpandProblemStatements", migrations[6], StringComparison.Ordinal);
        Assert.EndsWith("_AddProblemAssets", migrations[7], StringComparison.Ordinal);
        Assert.EndsWith("_ExpandProblemGenerationState", migrations[8], StringComparison.Ordinal);
        var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE exams", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE judge_jobs", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE termination_metadata", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE problem_judge_profiles", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE problem_test_cases", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE problem_drafts", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE problem_generation_operations", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE generated_tests", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE problem_author_assignments", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE exam_problem_assignments", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE problem_assets", script, StringComparison.Ordinal);
    }

}
