using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CaveChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaveChangeRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BaseRevisionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CurrentProposalVersionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ApprovedRevisionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReviewerUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ReviewedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewerNotes = table.Column<string>(type: "text", nullable: true),
                    BaseGeographicScopeJson = table.Column<string>(type: "jsonb", nullable: true),
                    ProposedGeographicScopeJson = table.Column<string>(type: "jsonb", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveChangeRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaveChangeRequestStagedFiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ChangeRequestId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FileId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveChangeRequestStagedFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaveProposalVersions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ChangeRequestId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    ProposalJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveProposalVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_AccountId_Status_CreatedOn",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "Status", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_CaveId_Status",
                table: "CaveChangeRequests",
                columns: new[] { "CaveId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequestStagedFiles_ChangeRequestId_FileId",
                table: "CaveChangeRequestStagedFiles",
                columns: new[] { "ChangeRequestId", "FileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaveProposalVersions_ChangeRequestId_CreatedOn",
                table: "CaveProposalVersions",
                columns: new[] { "ChangeRequestId", "CreatedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaveChangeRequests");

            migrationBuilder.DropTable(
                name: "CaveChangeRequestStagedFiles");

            migrationBuilder.DropTable(
                name: "CaveProposalVersions");
        }
    }
}
