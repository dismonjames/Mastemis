using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mastemis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddProblemAuthoring : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "problem_drafts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                DefaultLocale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                TimeLimitMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                MemoryLimitBytes = table.Column<long>(type: "bigint", nullable: false),
                OutputLimitBytes = table.Column<long>(type: "bigint", nullable: false),
                Checker = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                MasSource = table.Column<string>(type: "text", nullable: false),
                MasSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_problem_drafts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "problem_package_exports",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                PackageSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ObjectId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Length = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_problem_package_exports", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "problem_package_imports",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                PackageSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_problem_package_imports", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "generated_test_sets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                GenerationOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false),
                Published = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_generated_test_sets", x => x.Id);
                table.ForeignKey(
                    name: "FK_generated_test_sets_problem_drafts_ProblemId",
                    column: x => x.ProblemId,
                    principalTable: "problem_drafts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "problem_generation_operations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                Seed = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                RuntimeVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                PrngAlgorithm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                PublishedTestSetId = table.Column<Guid>(type: "uuid", nullable: true),
                ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_problem_generation_operations", x => x.Id);
                table.ForeignKey(
                    name: "FK_problem_generation_operations_problem_drafts_ProblemId",
                    column: x => x.ProblemId,
                    principalTable: "problem_drafts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "problem_statements",
            columns: table => new
            {
                ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                ObjectId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Length = table.Column<long>(type: "bigint", nullable: false),
                Revision = table.Column<int>(type: "integer", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_problem_statements", x => new { x.ProblemId, x.Locale });
                table.ForeignKey(
                    name: "FK_problem_statements_problem_drafts_ProblemId",
                    column: x => x.ProblemId,
                    principalTable: "problem_drafts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "generated_tests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TestSetId = table.Column<Guid>(type: "uuid", nullable: false),
                TestIndex = table.Column<int>(type: "integer", nullable: false),
                Group = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Checker = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                InputObjectId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                InputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                InputLength = table.Column<long>(type: "bigint", nullable: false),
                OutputObjectId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                OutputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                OutputLength = table.Column<long>(type: "bigint", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_generated_tests", x => x.Id);
                table.ForeignKey(
                    name: "FK_generated_tests_generated_test_sets_TestSetId",
                    column: x => x.TestSetId,
                    principalTable: "generated_test_sets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_generated_test_sets_GenerationOperationId",
            table: "generated_test_sets",
            column: "GenerationOperationId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_generated_test_sets_ProblemId_Published",
            table: "generated_test_sets",
            columns: new[] { "ProblemId", "Published" });

        migrationBuilder.CreateIndex(
            name: "IX_generated_test_sets_ProblemId_Version",
            table: "generated_test_sets",
            columns: new[] { "ProblemId", "Version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_generated_tests_TestSetId_TestIndex",
            table: "generated_tests",
            columns: new[] { "TestSetId", "TestIndex" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_problem_drafts_UpdatedAtUtc",
            table: "problem_drafts",
            column: "UpdatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_problem_generation_operations_ProblemId",
            table: "problem_generation_operations",
            column: "ProblemId",
            unique: true,
            filter: "\"Status\" IN (0, 1)");

        migrationBuilder.CreateIndex(
            name: "IX_problem_generation_operations_ProblemId_Status",
            table: "problem_generation_operations",
            columns: new[] { "ProblemId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_problem_package_exports_ProblemId_CreatedAtUtc",
            table: "problem_package_exports",
            columns: new[] { "ProblemId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_problem_package_imports_IdempotencyKey",
            table: "problem_package_imports",
            column: "IdempotencyKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_problem_package_imports_PackageSha256",
            table: "problem_package_imports",
            column: "PackageSha256");

        migrationBuilder.CreateIndex(
            name: "IX_problem_statements_ProblemId_UpdatedAtUtc",
            table: "problem_statements",
            columns: new[] { "ProblemId", "UpdatedAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "generated_tests");

        migrationBuilder.DropTable(
            name: "problem_generation_operations");

        migrationBuilder.DropTable(
            name: "problem_package_exports");

        migrationBuilder.DropTable(
            name: "problem_package_imports");

        migrationBuilder.DropTable(
            name: "problem_statements");

        migrationBuilder.DropTable(
            name: "generated_test_sets");

        migrationBuilder.DropTable(
            name: "problem_drafts");
    }
}
