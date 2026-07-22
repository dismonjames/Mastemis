using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mastemis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ExpandProblemGenerationState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_problem_generation_operations_ProblemId",
            table: "problem_generation_operations");

        migrationBuilder.AddColumn<Guid>(
            name: "ActorUserId",
            table: "problem_generation_operations",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CancellationRequestedAtUtc",
            table: "problem_generation_operations",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DiagnosticSummary",
            table: "problem_generation_operations",
            type: "character varying(4096)",
            maxLength: 4096,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DraftVersion",
            table: "problem_generation_operations",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "ExpectedOutputCount",
            table: "problem_generation_operations",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "GeneratedInputCount",
            table: "problem_generation_operations",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "MasSourceSha256",
            table: "problem_generation_operations",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "ProgressDenominator",
            table: "problem_generation_operations",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "ProgressNumerator",
            table: "problem_generation_operations",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "RequestedTestCount",
            table: "problem_generation_operations",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "StartedAtUtc",
            table: "problem_generation_operations",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "UpdatedAtUtc",
            table: "problem_generation_operations",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

        migrationBuilder.AddColumn<int>(
            name: "Version",
            table: "problem_drafts",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateIndex(
            name: "IX_problem_generation_operations_ProblemId",
            table: "problem_generation_operations",
            column: "ProblemId",
            unique: true,
            filter: "\"Status\" IN (0, 1, 2, 3, 4, 7)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_problem_generation_operations_ProblemId",
            table: "problem_generation_operations");

        migrationBuilder.DropColumn(
            name: "ActorUserId",
            table: "problem_generation_operations");

        migrationBuilder.DropColumn(
            name: "CancellationRequestedAtUtc",
            table: "problem_generation_operations");

        migrationBuilder.DropColumn(
            name: "DiagnosticSummary",
            table: "problem_generation_operations");

        migrationBuilder.DropColumn(
            name: "DraftVersion",
            table: "problem_generation_operations");

        migrationBuilder.DropColumn(
            name: "ExpectedOutputCount",
            table: "problem_generation_operations");

        migrationBuilder.DropColumn(
            name: "GeneratedInputCount",
            table: "problem_generation_operations");

        migrationBuilder.DropColumn(
            name: "MasSourceSha256",
            table: "problem_generation_operations");

        migrationBuilder.DropColumn(
            name: "ProgressDenominator",
            table: "problem_generation_operations");

        migrationBuilder.DropColumn(
            name: "ProgressNumerator",
            table: "problem_generation_operations");

        migrationBuilder.DropColumn(
            name: "RequestedTestCount",
            table: "problem_generation_operations");

        migrationBuilder.DropColumn(
            name: "StartedAtUtc",
            table: "problem_generation_operations");

        migrationBuilder.DropColumn(
            name: "UpdatedAtUtc",
            table: "problem_generation_operations");

        migrationBuilder.DropColumn(
            name: "Version",
            table: "problem_drafts");

        migrationBuilder.CreateIndex(
            name: "IX_problem_generation_operations_ProblemId",
            table: "problem_generation_operations",
            column: "ProblemId",
            unique: true,
            filter: "\"Status\" IN (0, 1)");
    }
}
