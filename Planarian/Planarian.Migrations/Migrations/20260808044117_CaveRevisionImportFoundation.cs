using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CaveRevisionImportFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Caves_AccountId",
                table: "Caves");

            migrationBuilder.AddColumn<string>(
                name: "CurrentRevisionId",
                table: "Caves",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Caves",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

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
                    table.UniqueConstraint("AK_CaveImportBatches_AccountId_Id", x => new { x.AccountId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "CaveChangeRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BaseRevisionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CurrentProposalVersionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ApprovedRevisionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReviewerUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ReviewedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewerNotes = table.Column<string>(type: "text", nullable: true),
                    BaseStateId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    BaseCountyId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ProposedStateId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ProposedCountyId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveChangeRequests", x => x.Id);
                    table.UniqueConstraint("AK_CaveChangeRequests_AccountId_Id", x => new { x.AccountId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "CaveChangeRequestStagedFiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
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
                    table.ForeignKey(
                        name: "FK_CaveChangeRequestStagedFiles_CaveChangeRequests_AccountId_C~",
                        columns: x => new { x.AccountId, x.ChangeRequestId },
                        principalTable: "CaveChangeRequests",
                        principalColumns: new[] { "AccountId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveChangeRequestStagedFiles_Files_FileId",
                        column: x => x.FileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaveProposalVersions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ChangeRequestId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PreviousProposalVersionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
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
                    table.UniqueConstraint("AK_CaveProposalVersions_AccountId_Id", x => new { x.AccountId, x.Id });
                    table.ForeignKey(
                        name: "FK_CaveProposalVersions_CaveChangeRequests_AccountId_ChangeReq~",
                        columns: x => new { x.AccountId, x.ChangeRequestId },
                        principalTable: "CaveChangeRequests",
                        principalColumns: new[] { "AccountId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveProposalVersions_CaveProposalVersions_AccountId_Previou~",
                        columns: x => new { x.AccountId, x.PreviousProposalVersionId },
                        principalTable: "CaveProposalVersions",
                        principalColumns: new[] { "AccountId", "Id" },
                        onDelete: ReferentialAction.Restrict);
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
                    table.UniqueConstraint("AK_CaveRevisions_AccountId_Id", x => new { x.AccountId, x.Id });
                    table.ForeignKey(
                        name: "FK_CaveRevisions_CaveChangeRequests_AccountId_ChangeRequestId",
                        columns: x => new { x.AccountId, x.ChangeRequestId },
                        principalTable: "CaveChangeRequests",
                        principalColumns: new[] { "AccountId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveRevisions_CaveImportBatches_AccountId_ImportBatchId",
                        columns: x => new { x.AccountId, x.ImportBatchId },
                        principalTable: "CaveImportBatches",
                        principalColumns: new[] { "AccountId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveRevisions_CaveRevisions_AccountId_PreviousRevisionId",
                        columns: x => new { x.AccountId, x.PreviousRevisionId },
                        principalTable: "CaveRevisions",
                        principalColumns: new[] { "AccountId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Caves_AccountId_CurrentRevisionId",
                table: "Caves",
                columns: new[] { "AccountId", "CurrentRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_AccountId_ApprovedRevisionId",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "ApprovedRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_AccountId_BaseRevisionId",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "BaseRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_AccountId_CurrentProposalVersionId",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "CurrentProposalVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_AccountId_Status_CreatedOn",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "Status", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_CaveId_Status",
                table: "CaveChangeRequests",
                columns: new[] { "CaveId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequestStagedFiles_AccountId_ChangeRequestId",
                table: "CaveChangeRequestStagedFiles",
                columns: new[] { "AccountId", "ChangeRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequestStagedFiles_ChangeRequestId_FileId",
                table: "CaveChangeRequestStagedFiles",
                columns: new[] { "ChangeRequestId", "FileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequestStagedFiles_FileId",
                table: "CaveChangeRequestStagedFiles",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveImportBatches_AccountId_CreatedOn",
                table: "CaveImportBatches",
                columns: new[] { "AccountId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveProposalVersions_AccountId_ChangeRequestId",
                table: "CaveProposalVersions",
                columns: new[] { "AccountId", "ChangeRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveProposalVersions_AccountId_PreviousProposalVersionId",
                table: "CaveProposalVersions",
                columns: new[] { "AccountId", "PreviousProposalVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveProposalVersions_ChangeRequestId_CreatedOn",
                table: "CaveProposalVersions",
                columns: new[] { "ChangeRequestId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_AccountId_CaveId_CreatedOn",
                table: "CaveRevisions",
                columns: new[] { "AccountId", "CaveId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_AccountId_ChangeRequestId",
                table: "CaveRevisions",
                columns: new[] { "AccountId", "ChangeRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_AccountId_ImportBatchId",
                table: "CaveRevisions",
                columns: new[] { "AccountId", "ImportBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_AccountId_PreviousRevisionId",
                table: "CaveRevisions",
                columns: new[] { "AccountId", "PreviousRevisionId" });

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
                name: "FK_Caves_CaveRevisions_AccountId_CurrentRevisionId",
                table: "Caves",
                columns: new[] { "AccountId", "CurrentRevisionId" },
                principalTable: "CaveRevisions",
                principalColumns: new[] { "AccountId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveChangeRequests_CaveProposalVersions_AccountId_CurrentPr~",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "CurrentProposalVersionId" },
                principalTable: "CaveProposalVersions",
                principalColumns: new[] { "AccountId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveChangeRequests_CaveRevisions_AccountId_ApprovedRevision~",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "ApprovedRevisionId" },
                principalTable: "CaveRevisions",
                principalColumns: new[] { "AccountId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveChangeRequests_CaveRevisions_AccountId_BaseRevisionId",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "BaseRevisionId" },
                principalTable: "CaveRevisions",
                principalColumns: new[] { "AccountId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Caves_CaveRevisions_AccountId_CurrentRevisionId",
                table: "Caves");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveChangeRequests_CaveProposalVersions_AccountId_CurrentPr~",
                table: "CaveChangeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveChangeRequests_CaveRevisions_AccountId_ApprovedRevision~",
                table: "CaveChangeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveChangeRequests_CaveRevisions_AccountId_BaseRevisionId",
                table: "CaveChangeRequests");

            migrationBuilder.DropTable(
                name: "CaveChangeRequestStagedFiles");

            migrationBuilder.DropTable(
                name: "CaveProposalVersions");

            migrationBuilder.DropTable(
                name: "CaveRevisions");

            migrationBuilder.DropTable(
                name: "CaveChangeRequests");

            migrationBuilder.DropTable(
                name: "CaveImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_Caves_AccountId_CurrentRevisionId",
                table: "Caves");

            migrationBuilder.DropColumn(
                name: "CurrentRevisionId",
                table: "Caves");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Caves");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_AccountId",
                table: "Caves",
                column: "AccountId");
        }
    }
}
