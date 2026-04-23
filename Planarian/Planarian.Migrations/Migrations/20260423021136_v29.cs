using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v29 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CavePermission_UserId_AccountId_PermissionId",
                table: "CavePermission");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CavePermission_CountyOrCave",
                table: "CavePermission");

            migrationBuilder.AddColumn<string>(
                name: "StateId",
                table: "CavePermission",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CavePermission_StateId",
                table: "CavePermission",
                column: "StateId");

            migrationBuilder.CreateIndex(
                name: "IX_CavePermission_UserId_AccountId_PermissionId",
                table: "CavePermission",
                columns: new[] { "UserId", "AccountId", "PermissionId" },
                unique: true,
                filter: "\"StateId\" IS NULL AND \"CountyId\" IS NULL AND \"CaveId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CavePermission_UserId_AccountId_StateId_PermissionId",
                table: "CavePermission",
                columns: new[] { "UserId", "AccountId", "StateId", "PermissionId" },
                unique: true,
                filter: "\"StateId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CavePermission_OneLocation",
                table: "CavePermission",
                sql: "((CASE WHEN \"StateId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"CountyId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"CaveId\" IS NULL THEN 0 ELSE 1 END)) <= 1");

            migrationBuilder.AddForeignKey(
                name: "FK_CavePermission_States_StateId",
                table: "CavePermission",
                column: "StateId",
                principalTable: "States",
                principalColumn: "Id");
            // UserCavePermissions
            migrationBuilder.Sql("""
            DROP VIEW IF EXISTS "UserCavePermissions";
            """);

            migrationBuilder.Sql("""
            CREATE
            OR REPLACE VIEW "UserCavePermissions" AS
            SELECT cp."AccountId",
                   cp."UserId",
                   c."Id" AS "CaveId",
                   NULL::character varying AS "CountyId",
                   p."Key"                 AS "PermissionKey",
                   p."Id"                  AS "PermissionId",
                   c."StateId"             AS "StateId"
            FROM "CavePermission" cp
                JOIN "Caves" c
            ON c."AccountId"::text = cp."AccountId"::text AND
                (cp."CaveId" IS NOT NULL AND cp."CaveId"::text = c."Id"::text OR
                cp."CountyId" IS NOT NULL AND cp."CountyId"::text = c."CountyId"::text OR
                cp."StateId" IS NOT NULL AND cp."StateId"::text = c."StateId"::text OR
                cp."CaveId" IS NULL AND cp."CountyId" IS NULL AND cp."StateId" IS NULL)
                JOIN "Permissions" p ON cp."PermissionId"::text = p."Id"::text
            WHERE p."PermissionType"::text = 'Cave'::text
            UNION
            SELECT cp."AccountId",
                   cp."UserId",
                   NULL::character varying AS "CaveId",
                   ct."Id"                 AS "CountyId",
                   p."Key"                 AS "PermissionKey",
                   p."Id"                  AS "PermissionId",
                   ct."StateId"            AS "StateId"
            FROM "CavePermission" cp
                JOIN "Counties" ct
            ON ct."AccountId"::text = cp."AccountId"::text AND
                (cp."CountyId" IS NOT NULL AND cp."CountyId"::text = ct."Id"::text OR
                cp."StateId" IS NOT NULL AND cp."StateId"::text = ct."StateId"::text OR
                cp."CaveId" IS NULL AND cp."CountyId" IS NULL AND cp."StateId" IS NULL)
                JOIN "Permissions" p ON cp."PermissionId"::text = p."Id"::text
            WHERE p."PermissionType"::text = 'Cave'::text
            UNION
            SELECT up."AccountId",
                   up."UserId",
                   c."Id" AS "CaveId",
                   NULL::character varying AS "CountyId",
                   p."Key"                 AS "PermissionKey",
                   p."Id"                  AS "PermissionId",
                   c."StateId"             AS "StateId"
            FROM "UserPermissions" up
                JOIN "Caves" c
            ON c."AccountId"::text = up."AccountId"::text
                CROSS JOIN "Permissions" p
            WHERE (up."PermissionId"::text IN (SELECT "Permissions"."Id"
                FROM "Permissions"
                WHERE "Permissions"."Key"::text = 'Admin'::text))
              AND p."PermissionType"::text = 'Cave'::text
            UNION
            SELECT up."AccountId",
                   up."UserId",
                   NULL::character varying AS "CaveId",
                   ct."Id"                 AS "CountyId",
                   p."Key"                 AS "PermissionKey",
                   p."Id"                  AS "PermissionId",
                   ct."StateId"            AS "StateId"
            FROM "UserPermissions" up
                JOIN "Counties" ct
            ON ct."AccountId"::text = up."AccountId"::text
                CROSS JOIN "Permissions" p
            WHERE (up."PermissionId"::text IN (SELECT "Permissions"."Id"
                FROM "Permissions"
                WHERE "Permissions"."Key"::text = 'Admin'::text))
              AND p."PermissionType"::text = 'Cave'::text;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // UserCavePermissions
            migrationBuilder.Sql("""
            DROP VIEW IF EXISTS "UserCavePermissions";
            """);

            migrationBuilder.Sql("""
            CREATE
            OR REPLACE VIEW "UserCavePermissions" AS
            SELECT cp."AccountId",
                cp."UserId",
                c."Id" AS "CaveId",
                NULL::character varying AS "CountyId",
                p."Key" AS "PermissionKey",
                p."Id" AS "PermissionId"
            FROM "CavePermission" cp
                JOIN "Caves" c ON c."AccountId"::text = cp."AccountId"::text AND (cp."CaveId" IS NOT NULL AND cp."CaveId"::text = c."Id"::text OR cp."CountyId" IS NOT NULL AND cp."CountyId"::text = c."CountyId"::text OR cp."CaveId" IS NULL AND cp."CountyId" IS NULL)
                JOIN "Permissions" p ON cp."PermissionId"::text = p."Id"::text
            WHERE p."PermissionType"::text = 'Cave'::text
            UNION
            SELECT cp."AccountId",
                cp."UserId",
                NULL::character varying AS "CaveId",
                ct."Id" AS "CountyId",
                p."Key" AS "PermissionKey",
                p."Id" AS "PermissionId"
            FROM "CavePermission" cp
                JOIN "Counties" ct ON ct."AccountId"::text = cp."AccountId"::text AND (cp."CountyId" IS NOT NULL AND cp."CountyId"::text = ct."Id"::text OR cp."CaveId" IS NULL AND cp."CountyId" IS NULL)
                JOIN "Permissions" p ON cp."PermissionId"::text = p."Id"::text
            WHERE p."PermissionType"::text = 'Cave'::text
            UNION
            SELECT up."AccountId",
                up."UserId",
                c."Id" AS "CaveId",
                NULL::character varying AS "CountyId",
                p."Key" AS "PermissionKey",
                p."Id" AS "PermissionId"
            FROM "UserPermissions" up
                JOIN "Caves" c ON c."AccountId"::text = up."AccountId"::text
                CROSS JOIN "Permissions" p
            WHERE (up."PermissionId"::text IN ( SELECT "Permissions"."Id"
                    FROM "Permissions"
                    WHERE "Permissions"."Key"::text = 'Admin'::text)) AND p."PermissionType"::text = 'Cave'::text
            UNION
            SELECT up."AccountId",
                up."UserId",
                NULL::character varying AS "CaveId",
                ct."Id" AS "CountyId",
                p."Key" AS "PermissionKey",
                p."Id" AS "PermissionId"
            FROM "UserPermissions" up
                JOIN "Counties" ct ON ct."AccountId"::text = up."AccountId"::text
                CROSS JOIN "Permissions" p
            WHERE (up."PermissionId"::text IN ( SELECT "Permissions"."Id"
                    FROM "Permissions"
                    WHERE "Permissions"."Key"::text = 'Admin'::text)) AND p."PermissionType"::text = 'Cave'::text
            UNION
            SELECT au."AccountId",
                au."UserId",
                c."Id" AS "CaveId",
                NULL::character varying AS "CountyId",
                p."Key" AS "PermissionKey",
                p."Id" AS "PermissionId"
            FROM "AccountUsers" au
                JOIN "Caves" c ON c."AccountId"::text = au."AccountId"::text
                CROSS JOIN ( SELECT "Permissions"."Id",
                        "Permissions"."Key"
                    FROM "Permissions"
                    WHERE "Permissions"."PermissionType"::text = 'Cave'::text) p
            WHERE (EXISTS ( SELECT 1
                    FROM "UserPermissions" up
                        JOIN "Permissions" pu ON up."PermissionId"::text = pu."Id"::text
                    WHERE up."UserId"::text = au."UserId"::text AND pu."Key"::text = 'PlanarianAdmin'::text))
            UNION
            SELECT au."AccountId",
                au."UserId",
                NULL::character varying AS "CaveId",
                ct."Id" AS "CountyId",
                p."Key" AS "PermissionKey",
                p."Id" AS "PermissionId"
            FROM "AccountUsers" au
                JOIN "Counties" ct ON ct."AccountId"::text = au."AccountId"::text
                CROSS JOIN ( SELECT "Permissions"."Id",
                        "Permissions"."Key"
                    FROM "Permissions"
                    WHERE "Permissions"."PermissionType"::text = 'Cave'::text) p
            WHERE (EXISTS ( SELECT 1
                    FROM "UserPermissions" up
                        JOIN "Permissions" pu ON up."PermissionId"::text = pu."Id"::text
                    WHERE up."UserId"::text = au."UserId"::text AND pu."Key"::text = 'PlanarianAdmin'::text));            
            """);

            migrationBuilder.DropForeignKey(
                name: "FK_CavePermission_States_StateId",
                table: "CavePermission");

            migrationBuilder.DropIndex(
                name: "IX_CavePermission_StateId",
                table: "CavePermission");

            migrationBuilder.DropIndex(
                name: "IX_CavePermission_UserId_AccountId_PermissionId",
                table: "CavePermission");

            migrationBuilder.DropIndex(
                name: "IX_CavePermission_UserId_AccountId_StateId_PermissionId",
                table: "CavePermission");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CavePermission_OneLocation",
                table: "CavePermission");

            migrationBuilder.DropColumn(
                name: "StateId",
                table: "CavePermission");

            migrationBuilder.CreateIndex(
                name: "IX_CavePermission_UserId_AccountId_PermissionId",
                table: "CavePermission",
                columns: new[] { "UserId", "AccountId", "PermissionId" },
                unique: true,
                filter: "\"CountyId\" IS NULL AND \"CaveId\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CavePermission_CountyOrCave",
                table: "CavePermission",
                sql: "\"CountyId\" IS NULL OR \"CaveId\" IS NULL");
        }
    }
}
