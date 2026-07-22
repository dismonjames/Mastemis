using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mastemis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ExpandProblemMasMetadata : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "MasRevision",
            table: "problem_drafts",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "MasRuntimeVersion",
            table: "problem_drafts",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "mas-runtime-1.0");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "MasValidatedAtUtc",
            table: "problem_drafts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MasValidationJson",
            table: "problem_drafts",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MasRevision",
            table: "problem_drafts");

        migrationBuilder.DropColumn(
            name: "MasRuntimeVersion",
            table: "problem_drafts");

        migrationBuilder.DropColumn(
            name: "MasValidatedAtUtc",
            table: "problem_drafts");

        migrationBuilder.DropColumn(
            name: "MasValidationJson",
            table: "problem_drafts");
    }
}
