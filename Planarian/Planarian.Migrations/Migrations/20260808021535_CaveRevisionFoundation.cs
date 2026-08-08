using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CaveRevisionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentRevisionId",
                table: "Caves",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CaveImportBatches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SourceFileName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SyncExisting = table.Column<bool>(type: "boolean", nullable: false),
                    InsertedCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedCount = table.Column<int>(type: "integer", nullable: false),
                    DeletedCount = table.Column<int>(type: "integer", nullable: false),
                    NoChangeCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaveRevisions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PreviousRevisionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SnapshotSchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    ChangeRequestId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ImportBatchId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveRevisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Caves_CurrentRevisionId",
                table: "Caves",
                column: "CurrentRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveImportBatches_AccountId_CreatedOn",
                table: "CaveImportBatches",
                columns: new[] { "AccountId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_AccountId_CaveId_CreatedOn",
                table: "CaveRevisions",
                columns: new[] { "AccountId", "CaveId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_ChangeRequestId",
                table: "CaveRevisions",
                column: "ChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_ImportBatchId",
                table: "CaveRevisions",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_PreviousRevisionId",
                table: "CaveRevisions",
                column: "PreviousRevisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Caves_CaveRevisions_CurrentRevisionId",
                table: "Caves",
                column: "CurrentRevisionId",
                principalTable: "CaveRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Caves_CaveRevisions_CurrentRevisionId",
                table: "Caves");

            migrationBuilder.DropTable(
                name: "CaveImportBatches");

            migrationBuilder.DropTable(
                name: "CaveRevisions");

            migrationBuilder.DropIndex(
                name: "IX_Caves_CurrentRevisionId",
                table: "Caves");

            migrationBuilder.DropColumn(
                name: "CurrentRevisionId",
                table: "Caves");

        }
    }
}
