using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mastemis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddJudgeExecutionReporting : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CheckerDiagnosticSummary",
            table: "judgements",
            type: "character varying(1024)",
            maxLength: 1024,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CompilerDiagnosticSummary",
            table: "judgements",
            type: "character varying(4096)",
            maxLength: 4096,
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "ExecutionMilliseconds",
            table: "judgements",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<int>(
            name: "ExitCode",
            table: "judgements",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "FailedTestIndex",
            table: "judgements",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "JudgeVersion",
            table: "judgements",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<long>(
            name: "PeakMemoryBytes",
            table: "judgements",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RuntimeDiagnosticSummary",
            table: "judgements",
            type: "character varying(1024)",
            maxLength: 1024,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SandboxBackend",
            table: "judgements",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "Signal",
            table: "judgements",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "StandardErrorBytes",
            table: "judgements",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "StandardOutputBytes",
            table: "judgements",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<Guid>(
            name: "WorkerId",
            table: "judgements",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LanguagesJson",
            table: "judge_workers",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "SandboxBackend",
            table: "judge_workers",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "problem_judge_profiles",
            columns: table => new
            {
                ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                CpuMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                WallMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                MemoryBytes = table.Column<long>(type: "bigint", nullable: false),
                OutputBytes = table.Column<long>(type: "bigint", nullable: false),
                FileBytes = table.Column<long>(type: "bigint", nullable: false),
                ProcessCount = table.Column<int>(type: "integer", nullable: false),
                TestCount = table.Column<int>(type: "integer", nullable: false),
                CompilationMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                CompilationOutputBytes = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_problem_judge_profiles", x => x.ProblemId);
            });

        migrationBuilder.CreateTable(
            name: "problem_test_cases",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                TestIndex = table.Column<int>(type: "integer", nullable: false),
                InputObjectId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                ExpectedObjectId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                InputBytes = table.Column<long>(type: "bigint", nullable: false),
                ExpectedBytes = table.Column<long>(type: "bigint", nullable: false),
                CheckerId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_problem_test_cases", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_judgements_WorkerId_CompletedAtUtc",
            table: "judgements",
            columns: new[] { "WorkerId", "CompletedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_problem_test_cases_ProblemId_TestIndex",
            table: "problem_test_cases",
            columns: new[] { "ProblemId", "TestIndex" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "problem_judge_profiles");

        migrationBuilder.DropTable(
            name: "problem_test_cases");

        migrationBuilder.DropIndex(
            name: "IX_judgements_WorkerId_CompletedAtUtc",
            table: "judgements");

        migrationBuilder.DropColumn(
            name: "CheckerDiagnosticSummary",
            table: "judgements");

        migrationBuilder.DropColumn(
            name: "CompilerDiagnosticSummary",
            table: "judgements");

        migrationBuilder.DropColumn(
            name: "ExecutionMilliseconds",
            table: "judgements");

        migrationBuilder.DropColumn(
            name: "ExitCode",
            table: "judgements");

        migrationBuilder.DropColumn(
            name: "FailedTestIndex",
            table: "judgements");

        migrationBuilder.DropColumn(
            name: "JudgeVersion",
            table: "judgements");

        migrationBuilder.DropColumn(
            name: "PeakMemoryBytes",
            table: "judgements");

        migrationBuilder.DropColumn(
            name: "RuntimeDiagnosticSummary",
            table: "judgements");

        migrationBuilder.DropColumn(
            name: "SandboxBackend",
            table: "judgements");

        migrationBuilder.DropColumn(
            name: "Signal",
            table: "judgements");

        migrationBuilder.DropColumn(
            name: "StandardErrorBytes",
            table: "judgements");

        migrationBuilder.DropColumn(
            name: "StandardOutputBytes",
            table: "judgements");

        migrationBuilder.DropColumn(
            name: "WorkerId",
            table: "judgements");

        migrationBuilder.DropColumn(
            name: "LanguagesJson",
            table: "judge_workers");

        migrationBuilder.DropColumn(
            name: "SandboxBackend",
            table: "judge_workers");
    }
}
