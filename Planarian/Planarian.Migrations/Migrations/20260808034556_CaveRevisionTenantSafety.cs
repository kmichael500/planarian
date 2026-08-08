using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CaveRevisionTenantSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Caves_CaveRevisions_CurrentRevisionId",
                table: "Caves");

            migrationBuilder.DropIndex(
                name: "IX_Caves_AccountId",
                table: "Caves");

            migrationBuilder.DropIndex(
                name: "IX_Caves_CurrentRevisionId",
                table: "Caves");

            migrationBuilder.AddColumn<string>(
                name: "AccountId",
                table: "CaveProposalVersions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountId",
                table: "CaveChangeRequestStagedFiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""CaveProposalVersions"" p
                SET ""AccountId"" = r.""AccountId""
                FROM ""CaveChangeRequests"" r
                WHERE r.""Id"" = p.""ChangeRequestId"";
                UPDATE ""CaveChangeRequestStagedFiles"" f
                SET ""AccountId"" = r.""AccountId""
                FROM ""CaveChangeRequests"" r
                WHERE r.""Id"" = f.""ChangeRequestId"";");

            migrationBuilder.AlterColumn<string>(
                name: "AccountId",
                table: "CaveProposalVersions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountId",
                table: "CaveChangeRequestStagedFiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CurrentProposalVersionId",
                table: "CaveChangeRequests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "BaseRevisionId",
                table: "CaveChangeRequests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<string>(
                name: "BaseCountyId",
                table: "CaveChangeRequests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseStateId",
                table: "CaveChangeRequests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedCountyId",
                table: "CaveChangeRequests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedStateId",
                table: "CaveChangeRequests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CaveRevisions_AccountId_Id",
                table: "CaveRevisions",
                columns: new[] { "AccountId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CaveProposalVersions_AccountId_Id",
                table: "CaveProposalVersions",
                columns: new[] { "AccountId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CaveImportBatches_AccountId_Id",
                table: "CaveImportBatches",
                columns: new[] { "AccountId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CaveChangeRequests_AccountId_Id",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Caves_AccountId_CurrentRevisionId",
                table: "Caves",
                columns: new[] { "AccountId", "CurrentRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_AccountId_ImportBatchId",
                table: "CaveRevisions",
                columns: new[] { "AccountId", "ImportBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_AccountId_PreviousRevisionId",
                table: "CaveRevisions",
                columns: new[] { "AccountId", "PreviousRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveProposalVersions_AccountId_ChangeRequestId",
                table: "CaveProposalVersions",
                columns: new[] { "AccountId", "ChangeRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequestStagedFiles_AccountId_ChangeRequestId",
                table: "CaveChangeRequestStagedFiles",
                columns: new[] { "AccountId", "ChangeRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_AccountId_ApprovedRevisionId",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "ApprovedRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_AccountId_BaseRevisionId",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "BaseRevisionId" });

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

            migrationBuilder.AddForeignKey(
                name: "FK_CaveChangeRequestStagedFiles_CaveChangeRequests_AccountId_C~",
                table: "CaveChangeRequestStagedFiles",
                columns: new[] { "AccountId", "ChangeRequestId" },
                principalTable: "CaveChangeRequests",
                principalColumns: new[] { "AccountId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveProposalVersions_CaveChangeRequests_AccountId_ChangeReq~",
                table: "CaveProposalVersions",
                columns: new[] { "AccountId", "ChangeRequestId" },
                principalTable: "CaveChangeRequests",
                principalColumns: new[] { "AccountId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveRevisions_CaveImportBatches_AccountId_ImportBatchId",
                table: "CaveRevisions",
                columns: new[] { "AccountId", "ImportBatchId" },
                principalTable: "CaveImportBatches",
                principalColumns: new[] { "AccountId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveRevisions_CaveRevisions_AccountId_PreviousRevisionId",
                table: "CaveRevisions",
                columns: new[] { "AccountId", "PreviousRevisionId" },
                principalTable: "CaveRevisions",
                principalColumns: new[] { "AccountId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Caves_CaveRevisions_AccountId_CurrentRevisionId",
                table: "Caves",
                columns: new[] { "AccountId", "CurrentRevisionId" },
                principalTable: "CaveRevisions",
                principalColumns: new[] { "AccountId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaveChangeRequests_CaveRevisions_AccountId_ApprovedRevision~",
                table: "CaveChangeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveChangeRequests_CaveRevisions_AccountId_BaseRevisionId",
                table: "CaveChangeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveChangeRequestStagedFiles_CaveChangeRequests_AccountId_C~",
                table: "CaveChangeRequestStagedFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveProposalVersions_CaveChangeRequests_AccountId_ChangeReq~",
                table: "CaveProposalVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveRevisions_CaveImportBatches_AccountId_ImportBatchId",
                table: "CaveRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveRevisions_CaveRevisions_AccountId_PreviousRevisionId",
                table: "CaveRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_Caves_CaveRevisions_AccountId_CurrentRevisionId",
                table: "Caves");

            migrationBuilder.DropIndex(
                name: "IX_Caves_AccountId_CurrentRevisionId",
                table: "Caves");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CaveRevisions_AccountId_Id",
                table: "CaveRevisions");

            migrationBuilder.DropIndex(
                name: "IX_CaveRevisions_AccountId_ImportBatchId",
                table: "CaveRevisions");

            migrationBuilder.DropIndex(
                name: "IX_CaveRevisions_AccountId_PreviousRevisionId",
                table: "CaveRevisions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CaveProposalVersions_AccountId_Id",
                table: "CaveProposalVersions");

            migrationBuilder.DropIndex(
                name: "IX_CaveProposalVersions_AccountId_ChangeRequestId",
                table: "CaveProposalVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CaveImportBatches_AccountId_Id",
                table: "CaveImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_CaveChangeRequestStagedFiles_AccountId_ChangeRequestId",
                table: "CaveChangeRequestStagedFiles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CaveChangeRequests_AccountId_Id",
                table: "CaveChangeRequests");

            migrationBuilder.DropIndex(
                name: "IX_CaveChangeRequests_AccountId_ApprovedRevisionId",
                table: "CaveChangeRequests");

            migrationBuilder.DropIndex(
                name: "IX_CaveChangeRequests_AccountId_BaseRevisionId",
                table: "CaveChangeRequests");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "CaveProposalVersions");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "CaveChangeRequestStagedFiles");

            migrationBuilder.DropColumn(
                name: "BaseCountyId",
                table: "CaveChangeRequests");

            migrationBuilder.DropColumn(
                name: "BaseStateId",
                table: "CaveChangeRequests");

            migrationBuilder.DropColumn(
                name: "ProposedCountyId",
                table: "CaveChangeRequests");

            migrationBuilder.DropColumn(
                name: "ProposedStateId",
                table: "CaveChangeRequests");

            migrationBuilder.AlterColumn<string>(
                name: "CurrentProposalVersionId",
                table: "CaveChangeRequests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BaseRevisionId",
                table: "CaveChangeRequests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Caves_AccountId",
                table: "Caves",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_CurrentRevisionId",
                table: "Caves",
                column: "CurrentRevisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Caves_CaveRevisions_CurrentRevisionId",
                table: "Caves",
                column: "CurrentRevisionId",
                principalTable: "CaveRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
