using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class EnforcePermissionDomainIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Permissions"
                        GROUP BY "PermissionType", "Key"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot enforce permission identity because duplicate (PermissionType, Key) rows exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Permissions_PermissionType_Key",
                table: "Permissions");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_PermissionType_Key",
                table: "Permissions",
                columns: new[] { "PermissionType", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Permissions_PermissionType_Key",
                table: "Permissions");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_PermissionType_Key",
                table: "Permissions",
                columns: new[] { "PermissionType", "Key" });
        }
    }
}
