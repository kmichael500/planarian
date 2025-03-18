using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v17 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW ""UserCavePermissions"" AS
                SELECT
                    cp.""AccountId"",
                    cp.""UserId"",
                    c.""Id"" AS ""CaveId""
                FROM ""CavePermission"" cp
                JOIN ""Caves"" c
                    ON c.""AccountId"" = cp.""AccountId""
                        AND (
                            (cp.""CaveId"" IS NOT NULL AND cp.""CaveId"" = c.""Id"")
                            OR (cp.""CountyId"" IS NOT NULL AND cp.""CountyId"" = c.""CountyId"")
                            OR (cp.""CaveId"" IS NULL AND cp.""CountyId"" IS NULL)
                        )
                JOIN ""Permissions"" p
                    ON cp.""PermissionId"" = p.""Id""
                WHERE p.""PermissionType"" = 'Cave'
                  AND p.""Key"" IN ('View', 'CountyCoordinator')
                UNION
                SELECT
                    up.""AccountId"",
                    up.""UserId"",
                    c.""Id"" AS ""CaveId""
                FROM ""UserPermissions"" up
                JOIN ""Caves"" c
                    ON c.""AccountId"" = up.""AccountId""
                JOIN ""Permissions"" p
                    ON up.""PermissionId"" = p.""Id""
                WHERE p.""Key"" IN ('Admin', 'PlanarianAdmin');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS ""UserCavePermissions"";");
        }
    }
}
