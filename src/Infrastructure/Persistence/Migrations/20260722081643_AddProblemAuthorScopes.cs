using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mastemis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddProblemAuthorScopes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "exam_problem_assignments",
            columns: table => new
            {
                ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_problem_assignments", x => new { x.ExamId, x.ProblemId });
                table.ForeignKey(
                    name: "FK_exam_problem_assignments_exams_ExamId",
                    column: x => x.ExamId,
                    principalTable: "exams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_exam_problem_assignments_problem_drafts_ProblemId",
                    column: x => x.ProblemId,
                    principalTable: "problem_drafts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "problem_author_assignments",
            columns: table => new
            {
                ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Role = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_problem_author_assignments", x => new { x.ProblemId, x.UserId });
                table.ForeignKey(
                    name: "FK_problem_author_assignments_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_problem_author_assignments_problem_drafts_ProblemId",
                    column: x => x.ProblemId,
                    principalTable: "problem_drafts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_exam_problem_assignments_ProblemId",
            table: "exam_problem_assignments",
            column: "ProblemId");

        migrationBuilder.CreateIndex(
            name: "IX_problem_author_assignments_UserId_Status_ExpiresAtUtc",
            table: "problem_author_assignments",
            columns: new[] { "UserId", "Status", "ExpiresAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "exam_problem_assignments");

        migrationBuilder.DropTable(
            name: "problem_author_assignments");
    }
}
