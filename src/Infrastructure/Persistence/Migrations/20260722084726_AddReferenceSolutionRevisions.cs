using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mastemis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddReferenceSolutionRevisions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "reference_solution_revisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                Language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                CompileProfile = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_reference_solution_revisions", x => x.Id);
                table.ForeignKey(
                    name: "FK_reference_solution_revisions_problem_drafts_ProblemId",
                    column: x => x.ProblemId,
                    principalTable: "problem_drafts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "reference_solution_sources",
            columns: table => new
            {
                RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                FileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ObjectId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Length = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_reference_solution_sources", x => new { x.RevisionId, x.FileName });
                table.ForeignKey(
                    name: "FK_reference_solution_sources_reference_solution_revisions_Rev~",
                    column: x => x.RevisionId,
                    principalTable: "reference_solution_revisions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_reference_solution_revisions_ProblemId",
            table: "reference_solution_revisions",
            column: "ProblemId",
            unique: true,
            filter: "\"IsCurrent\" = TRUE");

        migrationBuilder.CreateIndex(
            name: "IX_reference_solution_revisions_ProblemId_CreatedAtUtc",
            table: "reference_solution_revisions",
            columns: new[] { "ProblemId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_reference_solution_sources_ObjectId",
            table: "reference_solution_sources",
            column: "ObjectId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "reference_solution_sources");

        migrationBuilder.DropTable(
            name: "reference_solution_revisions");
    }
}
