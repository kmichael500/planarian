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
                name: "AccessRevokedOn",
                table: "AccountUsers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Older application versions ignore this column and would restore revoked access.
            migrationBuilder.Sql("""
                DO $planarian$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "AccountUsers" WHERE "AccessRevokedOn" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot roll back v33 while revoked account memberships exist. Resolve revoked access before downgrading the application.';
                    END IF;
                END
                $planarian$;
                """);

            migrationBuilder.DropColumn(
                name: "AccessRevokedOn",
                table: "AccountUsers");
        }
    }
}
