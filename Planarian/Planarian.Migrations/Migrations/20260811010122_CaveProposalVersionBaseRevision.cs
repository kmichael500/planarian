using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CaveProposalVersionBaseRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseRevisionId",
                table: "CaveProposalVersions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaveId",
                table: "CaveProposalVersions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "CaveProposalVersions" AS proposal
                SET "BaseRevisionId" = request."BaseRevisionId",
                    "CaveId" = request."CaveId"
                FROM "CaveChangeRequests" AS request
                WHERE proposal."AccountId" = request."AccountId"
                  AND proposal."ChangeRequestId" = request."Id"
                """);

            migrationBuilder.AlterColumn<string>(
                name: "BaseRevisionId",
                table: "CaveProposalVersions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CaveId",
                table: "CaveProposalVersions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaveProposalVersions_AccountId_CaveId_BaseRevisionId",
                table: "CaveProposalVersions",
                columns: new[] { "AccountId", "CaveId", "BaseRevisionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CaveProposalVersions_CaveRevisions_AccountId_CaveId_BaseRev~",
                table: "CaveProposalVersions",
                columns: new[] { "AccountId", "CaveId", "BaseRevisionId" },
                principalTable: "CaveRevisions",
                principalColumns: new[] { "AccountId", "CaveId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaveProposalVersions_CaveRevisions_AccountId_CaveId_BaseRev~",
                table: "CaveProposalVersions");

            migrationBuilder.DropIndex(
                name: "IX_CaveProposalVersions_AccountId_CaveId_BaseRevisionId",
                table: "CaveProposalVersions");

            migrationBuilder.DropColumn(
                name: "BaseRevisionId",
                table: "CaveProposalVersions");

            migrationBuilder.DropColumn(
                name: "CaveId",
                table: "CaveProposalVersions");
        }
    }
}
