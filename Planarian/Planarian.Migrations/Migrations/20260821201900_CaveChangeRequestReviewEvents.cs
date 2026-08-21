using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CaveChangeRequestReviewEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaveChangeRequestReviewEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ChangeRequestId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ProposalVersionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveChangeRequestReviewEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaveChangeRequestReviewEvents_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveChangeRequestReviewEvents_CaveChangeRequests_AccountId_~",
                        columns: x => new { x.AccountId, x.ChangeRequestId },
                        principalTable: "CaveChangeRequests",
                        principalColumns: new[] { "AccountId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveChangeRequestReviewEvents_CaveProposalVersions_AccountI~",
                        columns: x => new { x.AccountId, x.ChangeRequestId, x.ProposalVersionId },
                        principalTable: "CaveProposalVersions",
                        principalColumns: new[] { "AccountId", "ChangeRequestId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveChangeRequestReviewEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequestReviewEvents_AccountId_ChangeRequestId_Cre~",
                table: "CaveChangeRequestReviewEvents",
                columns: new[] { "AccountId", "ChangeRequestId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequestReviewEvents_AccountId_ChangeRequestId_Pro~",
                table: "CaveChangeRequestReviewEvents",
                columns: new[] { "AccountId", "ChangeRequestId", "ProposalVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequestReviewEvents_ActorUserId",
                table: "CaveChangeRequestReviewEvents",
                column: "ActorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaveChangeRequestReviewEvents");
        }
    }
}
