using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CaveProposalIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviousProposalVersionId",
                table: "CaveProposalVersions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaveProposalVersions_AccountId_PreviousProposalVersionId",
                table: "CaveProposalVersions",
                columns: new[] { "AccountId", "PreviousProposalVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_AccountId_CurrentProposalVersionId",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "CurrentProposalVersionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CaveChangeRequests_CaveProposalVersions_AccountId_CurrentPr~",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "CurrentProposalVersionId" },
                principalTable: "CaveProposalVersions",
                principalColumns: new[] { "AccountId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveProposalVersions_CaveProposalVersions_AccountId_Previou~",
                table: "CaveProposalVersions",
                columns: new[] { "AccountId", "PreviousProposalVersionId" },
                principalTable: "CaveProposalVersions",
                principalColumns: new[] { "AccountId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaveChangeRequests_CaveProposalVersions_AccountId_CurrentPr~",
                table: "CaveChangeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveProposalVersions_CaveProposalVersions_AccountId_Previou~",
                table: "CaveProposalVersions");

            migrationBuilder.DropIndex(
                name: "IX_CaveProposalVersions_AccountId_PreviousProposalVersionId",
                table: "CaveProposalVersions");

            migrationBuilder.DropIndex(
                name: "IX_CaveChangeRequests_AccountId_CurrentProposalVersionId",
                table: "CaveChangeRequests");

            migrationBuilder.DropColumn(
                name: "PreviousProposalVersionId",
                table: "CaveProposalVersions");
        }
    }
}
