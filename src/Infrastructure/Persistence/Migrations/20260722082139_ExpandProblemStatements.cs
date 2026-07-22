using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mastemis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ExpandProblemStatements : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Title",
            table: "problem_statements",
            type: "character varying(300)",
            maxLength: 300,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<Guid>(
            name: "UpdatedByUserId",
            table: "problem_statements",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Title",
            table: "problem_statements");

        migrationBuilder.DropColumn(
            name: "UpdatedByUserId",
            table: "problem_statements");
    }
}
