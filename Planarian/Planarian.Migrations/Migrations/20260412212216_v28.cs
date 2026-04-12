using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v28 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ThrottleEventLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    OperationName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LimiterKeyType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Path = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NormalizedIdentifier = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Limit = table.Column<int>(type: "integer", nullable: false),
                    WindowSeconds = table.Column<int>(type: "integer", nullable: false),
                    RetryAfterSeconds = table.Column<int>(type: "integer", nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThrottleEventLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThrottleEventLogs_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ThrottleEventLogs_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ThrottleEventLogs_CreatedByUserId",
                table: "ThrottleEventLogs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ThrottleEventLogs_ModifiedByUserId",
                table: "ThrottleEventLogs",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ThrottleEventLogs_OccurredOn",
                table: "ThrottleEventLogs",
                column: "OccurredOn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ThrottleEventLogs");
        }
    }
}
