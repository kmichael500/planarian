using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v27 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaveChangeLog",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ChangedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PropertyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChangeValueType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ValueString = table.Column<string>(type: "text", nullable: true),
                    ValueInt = table.Column<int>(type: "integer", nullable: true),
                    ValueDouble = table.Column<double>(type: "double precision", nullable: true),
                    ValueBool = table.Column<bool>(type: "boolean", nullable: true),
                    ValueDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveChangeLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaveChangeLog_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CaveChangeLog_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CaveChangeLog_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CaveChangeLog_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CaveChangeRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReviewedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaveChangeRequests_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CaveChangeRequests_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CaveChangeRequests_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeLog_AccountId",
                table: "CaveChangeLog",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeLog_ApprovedByUserId",
                table: "CaveChangeLog",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeLog_CaveId",
                table: "CaveChangeLog",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeLog_ChangedByUserId",
                table: "CaveChangeLog",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_AccountId",
                table: "CaveChangeRequests",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_CaveId",
                table: "CaveChangeRequests",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_ReviewedByUserId",
                table: "CaveChangeRequests",
                column: "ReviewedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaveChangeLog");

            migrationBuilder.DropTable(
                name: "CaveChangeRequests");
        }
    }
}
