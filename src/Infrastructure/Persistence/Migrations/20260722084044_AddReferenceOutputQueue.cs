using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mastemis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddReferenceOutputQueue : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "reference_output_jobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                Language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ContractVersion = table.Column<int>(type: "integer", nullable: false),
                PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                Attempt = table.Column<int>(type: "integer", nullable: false),
                MaximumAttempts = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                AvailableAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                WorkerId = table.Column<Guid>(type: "uuid", nullable: true),
                LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_reference_output_jobs", x => x.Id);
                table.ForeignKey(
                    name: "FK_reference_output_jobs_problem_generation_operations_Operati~",
                    column: x => x.OperationId,
                    principalTable: "problem_generation_operations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_reference_output_jobs_OperationId",
            table: "reference_output_jobs",
            column: "OperationId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_reference_output_jobs_Status_AvailableAtUtc_CreatedAtUtc",
            table: "reference_output_jobs",
            columns: new[] { "Status", "AvailableAtUtc", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_reference_output_jobs_Status_LeaseExpiresAtUtc",
            table: "reference_output_jobs",
            columns: new[] { "Status", "LeaseExpiresAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "reference_output_jobs");
    }
}
