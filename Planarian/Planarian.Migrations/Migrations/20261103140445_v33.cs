using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v33 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AccessDisabledOn",
                table: "AccountUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessDisabledMessage",
                table: "AccountUsers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.DropTable(
                name: "MessageTypes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Older application versions ignore this column and would restore disabled access.
            migrationBuilder.Sql("""
                DO $planarian$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "AccountUsers" WHERE "AccessDisabledOn" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot roll back v33 while disabled account memberships exist. Resolve disabled access before downgrading the application.';
                    END IF;
                END
                $planarian$;
                """);

            migrationBuilder.CreateTable(
                name: "MessageTypes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Subject = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FromName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FromEmail = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Mjml = table.Column<string>(type: "text", nullable: true),
                    Html = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageTypes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MessageTypes_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageTypes_CreatedByUserId",
                table: "MessageTypes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageTypes_ModifiedByUserId",
                table: "MessageTypes",
                column: "ModifiedByUserId");

            migrationBuilder.DropColumn(
                name: "AccessDisabledMessage",
                table: "AccountUsers");

            migrationBuilder.DropColumn(
                name: "AccessDisabledOn",
                table: "AccountUsers");
        }
    }
}
