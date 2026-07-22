using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mastemis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ExpandProblemPackageExports : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ActorUserId",
            table: "problem_package_exports",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ExpiresAtUtc",
            table: "problem_package_exports",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

        migrationBuilder.AddColumn<string>(
            name: "FailureCode",
            table: "problem_package_exports",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FormatVersion",
            table: "problem_package_exports",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "IdempotencyKey",
            table: "problem_package_exports",
            type: "character varying(128)",
            maxLength: 128,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<bool>(
            name: "IncludeHidden",
            table: "problem_package_exports",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "ProblemVersion",
            table: "problem_package_exports",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "Status",
            table: "problem_package_exports",
            type: "character varying(24)",
            maxLength: 24,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_problem_package_exports_IdempotencyKey",
            table: "problem_package_exports",
            column: "IdempotencyKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_problem_package_exports_Status_ExpiresAtUtc",
            table: "problem_package_exports",
            columns: new[] { "Status", "ExpiresAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_problem_package_exports_IdempotencyKey",
            table: "problem_package_exports");

        migrationBuilder.DropIndex(
            name: "IX_problem_package_exports_Status_ExpiresAtUtc",
            table: "problem_package_exports");

        migrationBuilder.DropColumn(
            name: "ActorUserId",
            table: "problem_package_exports");

        migrationBuilder.DropColumn(
            name: "ExpiresAtUtc",
            table: "problem_package_exports");

        migrationBuilder.DropColumn(
            name: "FailureCode",
            table: "problem_package_exports");

        migrationBuilder.DropColumn(
            name: "FormatVersion",
            table: "problem_package_exports");

        migrationBuilder.DropColumn(
            name: "IdempotencyKey",
            table: "problem_package_exports");

        migrationBuilder.DropColumn(
            name: "IncludeHidden",
            table: "problem_package_exports");

        migrationBuilder.DropColumn(
            name: "ProblemVersion",
            table: "problem_package_exports");

        migrationBuilder.DropColumn(
            name: "Status",
            table: "problem_package_exports");
    }
}
