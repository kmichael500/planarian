using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v19 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            CREATE OR REPLACE VIEW "" UserCavePermissions "" AS 
            SELECT cp.""AccountId"",
       cp.""UserId"",
       c.""Id""                  AS ""CaveId"",
       NULL::character varying AS ""CountyId"",
       p.""Key""                 AS ""PermissionKey"",
       p.""Id""                  AS ""PermissionId""
FROM ""CavePermission"" cp
         JOIN ""Caves"" c ON c.""AccountId""::text = cp.""AccountId""::text AND
                           (cp.""CaveId"" IS NOT NULL AND cp.""CaveId""::text = c.""Id""::text OR
                            cp.""CountyId"" IS NOT NULL AND cp.""CountyId""::text = c.""CountyId""::text OR
                            cp.""CaveId"" IS NULL AND cp.""CountyId"" IS NULL)
         JOIN ""Permissions"" p ON cp.""PermissionId""::text = p.""Id""::text
WHERE p.""PermissionType""::text = 'Cave'::text
UNION
SELECT cp.""AccountId"",
       cp.""UserId"",
       NULL::character varying AS ""CaveId"",
       ct.""Id""                 AS ""CountyId"",
       p.""Key""                 AS ""PermissionKey"",
       p.""Id""                  AS ""PermissionId""
FROM ""CavePermission"" cp
         JOIN ""Counties"" ct ON ct.""AccountId""::text = cp.""AccountId""::text AND
                               (cp.""CountyId"" IS NOT NULL AND cp.""CountyId""::text = ct.""Id""::text OR
                                cp.""CaveId"" IS NULL AND cp.""CountyId"" IS NULL)
         JOIN ""Permissions"" p ON cp.""PermissionId""::text = p.""Id""::text
WHERE p.""PermissionType""::text = 'Cave'::text
UNION
SELECT up.""AccountId"",
       up.""UserId"",
       c.""Id""                  AS ""CaveId"",
       NULL::character varying AS ""CountyId"",
       p.""Key""                 AS ""PermissionKey"",
       p.""Id""                  AS ""PermissionId""
FROM ""UserPermissions"" up
         JOIN ""Caves"" c ON c.""AccountId""::text = up.""AccountId""::text
         CROSS JOIN ""Permissions"" p
WHERE (up.""PermissionId""::text IN (SELECT ""Permissions"".""Id""
                                   FROM ""Permissions""
                                   WHERE ""Permissions"".""Key""::text = 'Admin'::text))
  AND p.""PermissionType""::text = 'Cave'::text
UNION
SELECT up.""AccountId"",
       up.""UserId"",
       NULL::character varying AS ""CaveId"",
       ct.""Id""                 AS ""CountyId"",
       p.""Key""                 AS ""PermissionKey"",
       p.""Id""                  AS ""PermissionId""
FROM ""UserPermissions"" up
         JOIN ""Counties"" ct ON ct.""AccountId""::text = up.""AccountId""::text
         CROSS JOIN ""Permissions"" p
WHERE (up.""PermissionId""::text IN (SELECT ""Permissions"".""Id""
                                   FROM ""Permissions""
                                   WHERE ""Permissions"".""Key""::text = 'Admin'::text))
  AND p.""PermissionType""::text = 'Cave'::text
UNION
SELECT au.""AccountId"",
       au.""UserId"",
       c.""Id""                  AS ""CaveId"",
       NULL::character varying AS ""CountyId"",
       p.""Key""                 AS ""PermissionKey"",
       p.""Id""                  AS ""PermissionId""
FROM ""AccountUsers"" au
         JOIN ""Caves"" c ON c.""AccountId""::text = au.""AccountId""::text
         CROSS JOIN (SELECT ""Permissions"".""Id"",
                            ""Permissions"".""Key""
                     FROM ""Permissions""
                     WHERE ""Permissions"".""PermissionType""::text = 'Cave'::text) p
WHERE (EXISTS (SELECT 1
               FROM ""UserPermissions"" up
                        JOIN ""Permissions"" pu ON up.""PermissionId""::text = pu.""Id""::text
               WHERE up.""UserId""::text = au.""UserId""::text
                 AND pu.""Key""::text = 'PlanarianAdmin'::text))
UNION
SELECT au.""AccountId"",
       au.""UserId"",
       NULL::character varying AS ""CaveId"",
       ct.""Id""                 AS ""CountyId"",
       p.""Key""                 AS ""PermissionKey"",
       p.""Id""                  AS ""PermissionId""
FROM ""AccountUsers"" au
         JOIN ""Counties"" ct ON ct.""AccountId""::text = au.""AccountId""::text
         CROSS JOIN (SELECT ""Permissions"".""Id"",
                            ""Permissions"".""Key""
                     FROM ""Permissions""
                     WHERE ""Permissions"".""PermissionType""::text = 'Cave'::text) p
WHERE (EXISTS (SELECT 1
               FROM ""UserPermissions"" up
                        JOIN ""Permissions"" pu ON up.""PermissionId""::text = pu.""Id""::text
               WHERE up.""UserId""::text = au.""UserId""::text
                 AND pu.""Key""::text = 'PlanarianAdmin'::text))
            ");
        }   

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
                        migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW ""UserCavePermissions"" AS
                SELECT
                    cp.""AccountId"",
                    cp.""UserId"",
                    c.""Id"" AS ""CaveId"",
                    NULL :: varchar AS ""CountyId"",
                    p.""Key"" AS ""PermissionKey"",
                    p.""Id"" AS ""PermissionId""
                FROM
                    ""CavePermission"" cp
                        JOIN ""Caves"" c ON c.""AccountId"" = cp.""AccountId""
                        AND (
                              (
                                  cp.""CaveId"" IS NOT NULL
                                      AND cp.""CaveId"" = c.""Id""
                                  )
                                  OR (
                                  cp.""CountyId"" IS NOT NULL
                                      AND cp.""CountyId"" = c.""CountyId""
                                  )
                                  OR (
                                  cp.""CaveId"" IS NULL
                                      AND cp.""CountyId"" IS NULL
                                  )
                              )
                        JOIN ""Permissions"" p ON cp.""PermissionId"" = p.""Id""
                WHERE
                    p.""PermissionType"" = 'Cave'
                UNION
                -- Branch 2: County-level permissions from CavePermission
                SELECT
                    cp.""AccountId"",
                    cp.""UserId"",
                    NULL :: varchar AS ""CaveId"",
                    ct.""Id"" AS ""CountyId"",
                    p.""Key"" AS ""PermissionKey"",
                    p.""Id"" AS ""PermissionId""
                FROM
                    ""CavePermission"" cp
                        JOIN ""Counties"" ct ON ct.""AccountId"" = cp.""AccountId""
                        AND (
                              (
                                  cp.""CountyId"" IS NOT NULL
                                      AND cp.""CountyId"" = ct.""Id""
                                  )
                                  OR (
                                  cp.""CaveId"" IS NULL
                                      AND cp.""CountyId"" IS NULL
                                  )
                              )
                        JOIN ""Permissions"" p ON cp.""PermissionId"" = p.""Id""
                WHERE
                    p.""PermissionType"" = 'Cave'
                UNION
                -- Branch 3: Admin/Planarian Admin - Cave Level
                SELECT
                    up.""AccountId"",
                    up.""UserId"",
                    c.""Id"" AS ""CaveId"",
                    NULL :: varchar AS ""CountyId"",
                    p.""Key"" AS ""PermissionKey"",
                    p.""Id"" AS ""PermissionId""
                FROM
                    ""UserPermissions"" up
                        JOIN ""Caves"" c ON c.""AccountId"" = up.""AccountId"" CROSS
                        JOIN ""Permissions"" p
                WHERE
                    up.""PermissionId"" IN (
                        SELECT
                            ""Id""
                        FROM
                            ""Permissions""
                        WHERE
                            ""Key"" IN ('Admin', 'PlanarianAdmin')
                    )
                  AND p.""PermissionType"" = 'Cave'
                UNION
                -- Branch 4: Admin/Planarian Admin - County Level
                SELECT
                    up.""AccountId"",
                    up.""UserId"",
                    NULL :: varchar AS ""CaveId"",
                    ct.""Id"" AS ""CountyId"",
                    p.""Key"" AS ""PermissionKey"",
                    p.""Id"" AS ""PermissionId""
                FROM
                    ""UserPermissions"" up
                        JOIN ""Counties"" ct ON ct.""AccountId"" = up.""AccountId"" CROSS
                        JOIN ""Permissions"" p
                WHERE
                    up.""PermissionId"" IN (
                        SELECT
                            ""Id""
                        FROM
                            ""Permissions""
                        WHERE
                            ""Key"" IN ('Admin', 'PlanarianAdmin')
                    )
                  AND p.""PermissionType"" = 'Cave';
            ");

        }
    }
}
