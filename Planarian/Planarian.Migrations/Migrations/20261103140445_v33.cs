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

            migrationBuilder.DropColumn(
                name: "AccessDisabledMessage",
                table: "AccountUsers");

            migrationBuilder.DropColumn(
                name: "AccessDisabledOn",
                table: "AccountUsers");
        }
    }
}
