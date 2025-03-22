using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v20 : Migration
    {
        /// <inheritdoc />
        /// this does not apply automatically because there will be no permisions in the db
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create a new CavePermission for each User with the 'View' Permission
            migrationBuilder.Sql(@"
    INSERT INTO ""CavePermission"" (
        ""Id"", 
        ""UserId"", 
        ""AccountId"", 
        ""PermissionId"", 
        ""CountyId"", 
        ""CaveId"", 
        ""CreatedOn""
    )
    SELECT 
        r.random_id,
        au.""UserId"",
        au.""AccountId"",
        p.""Id"",
        NULL,
        NULL,
        NOW()
    FROM ""AccountUsers"" au
    JOIN ""Permissions"" p ON p.""Key"" = 'View'
    CROSS JOIN LATERAL (
        SELECT string_agg(
                 substring('ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789'
                           FROM floor(random() * 62)::int + 1
                           FOR 1),
                 ''
               ) AS random_id
        FROM generate_series(1,10)
        WHERE au.""UserId"" IS NOT NULL
    ) r
    WHERE NOT EXISTS (
        SELECT 1 
        FROM ""CavePermission"" cp
        WHERE cp.""UserId"" = au.""UserId""
          AND cp.""AccountId"" = au.""AccountId""
          AND cp.""PermissionId"" = p.""Id""
          AND cp.""CountyId"" IS NULL
          AND cp.""CaveId"" IS NULL
    );
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
