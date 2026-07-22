using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mastemis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ExpandProblemDraftMetadata : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AcceptedLanguagesJson",
            table: "problem_drafts",
            type: "jsonb",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "AuthorsJson",
            table: "problem_drafts",
            type: "jsonb",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Difficulty",
            table: "problem_drafts",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "TagsJson",
            table: "problem_drafts",
            type: "jsonb",
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AcceptedLanguagesJson",
            table: "problem_drafts");

        migrationBuilder.DropColumn(
            name: "AuthorsJson",
            table: "problem_drafts");

        migrationBuilder.DropColumn(
            name: "Difficulty",
            table: "problem_drafts");

        migrationBuilder.DropColumn(
            name: "TagsJson",
            table: "problem_drafts");
    }
}
