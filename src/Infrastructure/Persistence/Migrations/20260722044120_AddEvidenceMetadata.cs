using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mastemis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddEvidenceMetadata : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "evidence_packages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LatestChainHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_evidence_packages", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "evidence_items",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<int>(type: "integer", nullable: false),
                ServerTimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ObjectId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                PreviousChainHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                MetadataJson = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_evidence_items", x => x.Id);
                table.ForeignKey(
                    name: "FK_evidence_items_evidence_packages_PackageId",
                    column: x => x.PackageId,
                    principalTable: "evidence_packages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "evidence_review_grants",
            columns: table => new
            {
                PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                GrantedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_evidence_review_grants", x => new { x.PackageId, x.ReviewerId });
                table.ForeignKey(
                    name: "FK_evidence_review_grants_evidence_packages_PackageId",
                    column: x => x.PackageId,
                    principalTable: "evidence_packages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_evidence_items_PackageId_ServerTimestampUtc",
            table: "evidence_items",
            columns: new[] { "PackageId", "ServerTimestampUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_evidence_packages_ExamId_RoomId_CandidateId_CreatedAtUtc",
            table: "evidence_packages",
            columns: new[] { "ExamId", "RoomId", "CandidateId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_evidence_packages_SessionId",
            table: "evidence_packages",
            column: "SessionId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_evidence_review_grants_ReviewerId",
            table: "evidence_review_grants",
            column: "ReviewerId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "evidence_items");

        migrationBuilder.DropTable(
            name: "evidence_review_grants");

        migrationBuilder.DropTable(
            name: "evidence_packages");
    }
}
